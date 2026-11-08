using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi;
using RaceDay.Api.Data;
using RaceDay.Api.Middleware;
using RaceDay.Api.Security;
using RaceDay.Api.Services;
using RaceDay.Api.Swagger;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Database (EF Core, SQL Server)
builder.Services.AddDbContext<RaceDayDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("RaceDayDb");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "The connection string 'RaceDayDb' is missing. Add it under \"ConnectionStrings\" in appsettings.json (see the README).");

    options.UseSqlServer(connectionString);

    // Don't stop the API from starting if the model snapshot and the model differ slightly.
    options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddSingleton<IPasswordService, PasswordService>();

// Azure Blob Storage for event banners and profile pictures (configure AzureBlob:ConnectionString).
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

// Server-side session: stores the logged-in user's ID and role.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "Event management API for South African road running, walking and cycling events. " +
                      "Log in via POST /api/auth/login first - the session cookie is then sent automatically by Swagger UI."
    });

    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
        options.IncludeXmlComments(xmlFile);
});

var app = builder.Build();

// Development only: let Entity Framework create/update the database and add demo data.
// Skipped automatically when no migrations exist yet or when using the in-memory test database.
// SQL Server (especially in Docker) can need a few seconds before it accepts connections, so connection
// problems are retried; a database left over from an earlier schema is reported straight away.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

    if (db.Database.IsSqlServer() && db.Database.GetMigrations().Any())
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                db.Database.Migrate();
                DbSeeder.Seed(db, passwords);
                break;
            }
            catch (Exception ex) when (attempt < maxAttempts && !IsSchemaConflict(ex))
            {
                app.Logger.LogWarning("Database not ready yet (attempt {Attempt} of {Max}): {Message}", attempt, maxAttempts, ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, IsSchemaConflict(ex)
                    ? "The database already contains tables from an earlier version. Drop the 'RaceDayDB' database (or point ConnectionStrings:RaceDayDb at a new one) and start again."
                    : "Could not migrate or seed the database. Check ConnectionStrings:RaceDayDb in appsettings.json.");
                throw;
            }
        }
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger stays on in every environment so the API can be demonstrated and marked.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.DocumentTitle = "RaceDay API";
    options.HeadContent = SwaggerTheme.HeadContent;
});

// Disabled in Development so the MVC app (and Docker) can call the API over plain HTTP.
if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseSession();
app.MapControllers();

app.Run();

static bool IsSchemaConflict(Exception ex) =>
    ex.Message.Contains("already an object named", StringComparison.OrdinalIgnoreCase);

// Makes Program visible to the test project (WebApplicationFactory).
public partial class Program { }

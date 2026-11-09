using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>
/// Boots the real API in memory, swapping SQL Server for an in-memory database
/// so tests run anywhere (including GitHub Actions) without a SQL Server instance.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // ConfigureTestServices runs AFTER Program.cs has registered the SQL Server DbContext,
        // so we can remove it. (Plain ConfigureServices runs too early with minimal hosting.)
        builder.ConfigureTestServices(services =>
        {
            // Newer EF Core versions register the provider setup in two places, so remove both.
            // If SQL Server stays registered next to the in-memory provider, EF throws on startup.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>)
                            || d.ServiceType == typeof(DbContextOptions)
                            || (d.ServiceType.IsGenericType
                                && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                                && d.ServiceType.GenericTypeArguments[0] == typeof(RaceDayDbContext)))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(this._dbName));

            // Use a fake in place of Azure Blob Storage so image upload tests need no cloud account.
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, FakeBlobStorageService>();
        });
    }
}
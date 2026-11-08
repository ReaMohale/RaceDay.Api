using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Middleware;

/// <summary>
/// Last line of defence: an unexpected error becomes a clean JSON problem response (500)
/// instead of a stack trace, and the details go to the log.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        this._next = next;
        this._logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await this._next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller went away; there is nobody to answer.
        }
        catch (BadHttpRequestException ex)
        {
            // Malformed or oversized request (for example a body that is too large).
            await WriteProblemAsync(context, ex.StatusCode, "Bad request", ex.Message);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Server error",
                "Something went wrong on the server. Please try again.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}

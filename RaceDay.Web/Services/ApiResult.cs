namespace RaceDay.Web.Services;

/// <summary>The outcome of one call to the RaceDay API: data on success, a friendly message on failure.</summary>
public class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>Field-level validation messages from the API (field name -> messages).</summary>
    public Dictionary<string, string[]> Errors { get; init; } = new();

    public static ApiResult<T> Ok(T? data, int statusCode = 200) =>
        new() { Success = true, Data = data, StatusCode = statusCode };

    public static ApiResult<T> Fail(int statusCode, string message, Dictionary<string, string[]>? errors = null) =>
        new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors ?? new Dictionary<string, string[]>() };
}

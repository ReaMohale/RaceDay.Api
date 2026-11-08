using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// Talks to the RaceDay API over HTTP. The signed-in user's API session cookie is read from the MVC
/// session and attached to every request, so the API sees the right user and role.
/// </summary>
public class RaceDayApiClient : IRaceDayApi
{
    private const string ApiSessionCookieName = ".RaceDay.Session";
    private const string UnavailableMessage = "The RaceDay service could not be reached. Please try again in a moment.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor accessor)
    {
        this._http = http;
        this._accessor = accessor;
    }

    // ------------------------------------------------------------------ account

    public async Task<ApiResult<LoginData>> LoginAsync(string email, string password)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonContent.Create(new { email, password }, options: JsonOptions)
            };
            using var response = await this._http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return await ReadErrorAsync<LoginData>(response);

            var profile = await response.Content.ReadFromJsonAsync<UserProfile>(JsonOptions);

            string? cookie = null;
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                var raw = values.FirstOrDefault(v => v.StartsWith(ApiSessionCookieName + "=", StringComparison.Ordinal));
                if (raw is not null)
                    cookie = raw.Split(';')[0];
            }

            if (profile is null || cookie is null)
                return ApiResult<LoginData>.Fail(502, "Login worked but the service did not start a session. Please try again.");

            return ApiResult<LoginData>.Ok(new LoginData(profile, cookie));
        }
        catch (Exception)
        {
            return ApiResult<LoginData>.Fail(503, UnavailableMessage);
        }
    }

    public Task<ApiResult<UserProfile>> RegisterAsync(RegisterViewModel model) =>
        this.SendAsync<UserProfile>(HttpMethod.Post, "api/auth/register", Json(new
        {
            fullName = model.FullName,
            email = model.Email,
            password = model.Password,
            phoneNumber = model.PhoneNumber,
            accountType = model.AccountType
        }));

    public Task<ApiResult<bool>> LogoutAsync() =>
        this.SendAsync<bool>(HttpMethod.Post, "api/auth/logout");

    public Task<ApiResult<UserProfile>> GetMeAsync() =>
        this.SendAsync<UserProfile>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserProfile>> UpdateMeAsync(string fullName, string? phoneNumber) =>
        this.SendAsync<UserProfile>(HttpMethod.Put, "api/users/me", Json(new { fullName, phoneNumber }));

    public async Task<ApiResult<UserProfile>> UploadProfilePictureAsync(IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        content.Add(FileContent(stream, file), "File", file.FileName);
        return await this.SendAsync<UserProfile>(HttpMethod.Post, "api/users/me/picture", content);
    }

    // ------------------------------------------------------------------ events

    public Task<ApiResult<List<EventItem>>> GetEventsAsync(string? type, string? search, bool upcomingOnly)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(type))
            query.Add("type=" + Uri.EscapeDataString(type));
        if (!string.IsNullOrWhiteSpace(search))
            query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        if (upcomingOnly)
            query.Add("upcomingOnly=true");

        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return this.SendAsync<List<EventItem>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventItem>> GetEventAsync(int id) =>
        this.SendAsync<EventItem>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<EventItem>> CreateEventAsync(EventFormViewModel model) =>
        this.SendAsync<EventItem>(HttpMethod.Post, "api/events", Json(EventBody(model)));

    public Task<ApiResult<EventItem>> UpdateEventAsync(int id, EventFormViewModel model) =>
        this.SendAsync<EventItem>(HttpMethod.Put, $"api/events/{id}", Json(EventBody(model)));

    public Task<ApiResult<bool>> DeleteEventAsync(int id) =>
        this.SendAsync<bool>(HttpMethod.Delete, $"api/events/{id}");

    public async Task<ApiResult<EventItem>> UploadBannerAsync(int eventId, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        content.Add(FileContent(stream, file), "File", file.FileName);
        return await this.SendAsync<EventItem>(HttpMethod.Post, $"api/events/{eventId}/banner", content);
    }

    public Task<ApiResult<List<OrganiserEventSummary>>> GetMyEventsAsync() =>
        this.SendAsync<List<OrganiserEventSummary>>(HttpMethod.Get, "api/organisers/me/events");

    // ------------------------------------------------------------------ categories

    public Task<ApiResult<List<CategoryItem>>> GetCategoriesAsync(int eventId) =>
        this.SendAsync<List<CategoryItem>>(HttpMethod.Get, $"api/events/{eventId}/categories");

    public Task<ApiResult<CategoryItem>> CreateCategoryAsync(CategoryFormViewModel model) =>
        this.SendAsync<CategoryItem>(HttpMethod.Post, $"api/events/{model.EventId}/categories", Json(CategoryBody(model)));

    public Task<ApiResult<CategoryItem>> UpdateCategoryAsync(int categoryId, CategoryFormViewModel model) =>
        this.SendAsync<CategoryItem>(HttpMethod.Put, $"api/categories/{categoryId}", Json(CategoryBody(model)));

    public Task<ApiResult<bool>> DeleteCategoryAsync(int categoryId) =>
        this.SendAsync<bool>(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ------------------------------------------------------------------ enrolments

    public Task<ApiResult<EnrolmentItem>> EnrolAsync(int categoryId) =>
        this.SendAsync<EnrolmentItem>(HttpMethod.Post, $"api/categories/{categoryId}/enrolments");

    public Task<ApiResult<List<EnrolmentItem>>> GetMyEnrolmentsAsync() =>
        this.SendAsync<List<EnrolmentItem>>(HttpMethod.Get, "api/enrolments/me");

    public Task<ApiResult<List<EnrolmentItem>>> GetEventEnrolmentsAsync(int eventId) =>
        this.SendAsync<List<EnrolmentItem>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<bool>> CancelEnrolmentAsync(int enrolmentId) =>
        this.SendAsync<bool>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    // ------------------------------------------------------------------ results

    public Task<ApiResult<ResultItem>> CaptureResultAsync(int enrolmentId, TimeSpan finishTime, int? position) =>
        this.SendAsync<ResultItem>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/results",
            Json(new { finishTime, position }));

    public Task<ApiResult<List<ResultItem>>> GetEventResultsAsync(int eventId) =>
        this.SendAsync<List<ResultItem>>(HttpMethod.Get, $"api/events/{eventId}/results");

    public Task<ApiResult<List<ResultItem>>> GetMyResultsAsync() =>
        this.SendAsync<List<ResultItem>>(HttpMethod.Get, "api/participants/me/results");

    // ------------------------------------------------------------------ plumbing

    private static object EventBody(EventFormViewModel model) => new
    {
        eventName = model.EventName,
        description = model.Description,
        eventDate = model.EventDate,
        location = model.Location,
        eventType = model.EventType,
        distanceKm = model.DistanceKm
    };

    private static object CategoryBody(CategoryFormViewModel model) => new
    {
        categoryName = model.CategoryName,
        distanceKm = model.DistanceKm,
        entryFee = model.EntryFee,
        maxParticipants = model.MaxParticipants
    };

    private static HttpContent Json(object body) => JsonContent.Create(body, options: JsonOptions);

    private static StreamContent FileContent(Stream stream, IFormFile file)
    {
        var content = new StreamContent(stream);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    /// <summary>Sends a request with the user's API session cookie and turns the reply into an <see cref="ApiResult{T}"/>.</summary>
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };

            var cookie = this._accessor.HttpContext?.Session.GetString(SessionKeys.ApiCookie);
            if (!string.IsNullOrEmpty(cookie))
                request.Headers.TryAddWithoutValidation("Cookie", cookie);

            using var response = await this._http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return await ReadErrorAsync<T>(response);

            // Calls that return no data (logout, delete) are reported as "true".
            if (typeof(T) == typeof(bool))
                return ApiResult<T>.Ok((T)(object)true, (int)response.StatusCode);

            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return ApiResult<T>.Ok(data, (int)response.StatusCode);
        }
        catch (Exception)
        {
            return ApiResult<T>.Fail(503, UnavailableMessage);
        }
    }

    /// <summary>Reads the API's problem-details error and keeps only a message that is safe to show a user.</summary>
    private static async Task<ApiResult<T>> ReadErrorAsync<T>(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        var message = DefaultMessage(status);
        var errors = new Dictionary<string, string[]>();

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                        message = detail.GetString() ?? message;
                    else if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        message = title.GetString() ?? message;

                    if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in errorsElement.EnumerateObject())
                        {
                            if (property.Value.ValueKind != JsonValueKind.Array)
                                continue;

                            errors[property.Name] = property.Value.EnumerateArray()
                                .Select(item => item.GetString() ?? string.Empty)
                                .Where(text => text.Length > 0)
                                .ToArray();
                        }

                        if (errors.Count > 0)
                            message = "Please correct the highlighted details and try again.";
                    }
                }
            }
        }
        catch (Exception)
        {
            // An unreadable error body just means we show the default message for the status code.
        }

        return ApiResult<T>.Fail(status, message, errors);
    }

    private static string DefaultMessage(int status) => status switch
    {
        400 => "Some of the details are not valid. Please check them and try again.",
        401 => "Please log in to continue.",
        403 => "You do not have permission to do that.",
        404 => "We could not find what you were looking for.",
        409 => "That conflicts with existing information.",
        503 => UnavailableMessage,
        _ => "Something went wrong. Please try again."
    };
}

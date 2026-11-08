using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Every call the MVC app makes to the Part 2 API.</summary>
public interface IRaceDayApi
{
    // account
    Task<ApiResult<LoginData>> LoginAsync(string email, string password);
    Task<ApiResult<UserProfile>> RegisterAsync(RegisterViewModel model);
    Task<ApiResult<bool>> LogoutAsync();
    Task<ApiResult<UserProfile>> GetMeAsync();
    Task<ApiResult<UserProfile>> UpdateMeAsync(string fullName, string? phoneNumber);
    Task<ApiResult<UserProfile>> UploadProfilePictureAsync(IFormFile file);

    // events
    Task<ApiResult<List<EventItem>>> GetEventsAsync(string? type, string? search, bool upcomingOnly);
    Task<ApiResult<EventItem>> GetEventAsync(int id);
    Task<ApiResult<EventItem>> CreateEventAsync(EventFormViewModel model);
    Task<ApiResult<EventItem>> UpdateEventAsync(int id, EventFormViewModel model);
    Task<ApiResult<bool>> DeleteEventAsync(int id);
    Task<ApiResult<EventItem>> UploadBannerAsync(int eventId, IFormFile file);
    Task<ApiResult<List<OrganiserEventSummary>>> GetMyEventsAsync();

    // categories
    Task<ApiResult<List<CategoryItem>>> GetCategoriesAsync(int eventId);
    Task<ApiResult<CategoryItem>> CreateCategoryAsync(CategoryFormViewModel model);
    Task<ApiResult<CategoryItem>> UpdateCategoryAsync(int categoryId, CategoryFormViewModel model);
    Task<ApiResult<bool>> DeleteCategoryAsync(int categoryId);

    // enrolments
    Task<ApiResult<EnrolmentItem>> EnrolAsync(int categoryId);
    Task<ApiResult<List<EnrolmentItem>>> GetMyEnrolmentsAsync();
    Task<ApiResult<List<EnrolmentItem>>> GetEventEnrolmentsAsync(int eventId);
    Task<ApiResult<bool>> CancelEnrolmentAsync(int enrolmentId);

    // results
    Task<ApiResult<ResultItem>> CaptureResultAsync(int enrolmentId, TimeSpan finishTime, int? position);
    Task<ApiResult<List<ResultItem>>> GetEventResultsAsync(int eventId);
    Task<ApiResult<List<ResultItem>>> GetMyResultsAsync();
}

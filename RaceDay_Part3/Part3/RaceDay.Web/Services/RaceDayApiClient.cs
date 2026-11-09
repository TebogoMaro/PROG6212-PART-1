using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>The outcome of one API call. Errors are already turned into a friendly message.</summary>
public class ApiResult<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
}

public record ApiFile(byte[] Content, string ContentType);

/// <summary>
/// The ONLY way the web app reaches data: it calls the RaceDay API over HTTP.
/// After login the API's session cookie is stored in this app's web session and sent with every later call,
/// so the API still decides who the user is and what they may do.
/// </summary>
public class RaceDayApiClient
{
    private const string ApiCookieName = "RaceDay.Session=";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor accessor)
    {
        _http = http;
        _accessor = accessor;
    }

    private ISession Session => _accessor.HttpContext!.Session;

    // ======================= Accounts =======================

    public Task<ApiResult<UserDto>> RegisterAsync(string fullName, string email, string password, string role, string? organiserCode) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register",
            Json(new { fullName, email, password, role, organiserCode }));

    public Task<ApiResult<UserDto>> LoginAsync(string email, string password) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/login", Json(new { email, password }));

    public Task<ApiResult<object>> LogoutAsync() => SendAsync<object>(HttpMethod.Post, "api/auth/logout");

    public Task<ApiResult<UserDto>> GetMeAsync() => SendAsync<UserDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserDto>> UpdateMeAsync(string fullName, string email) =>
        SendAsync<UserDto>(HttpMethod.Put, "api/users/me", Json(new { fullName, email }));

    public Task<ApiResult<UserDto>> UploadPictureAsync(IFormFile file) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/users/me/picture", FileContent(file));

    public Task<ApiFile?> GetPictureAsync() => GetFileAsync("api/users/me/picture");

    // ======================= Events =======================

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(string? location, string? eventType, DateOnly? fromDate, DateOnly? toDate)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(location)) query.Add("location=" + Uri.EscapeDataString(location.Trim()));
        if (!string.IsNullOrWhiteSpace(eventType)) query.Add("eventType=" + Uri.EscapeDataString(eventType));
        if (fromDate is not null) query.Add("fromDate=" + fromDate.Value.ToString("yyyy-MM-dd"));
        if (toDate is not null) query.Add("toDate=" + toDate.Value.ToString("yyyy-MM-dd"));
        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return SendAsync<List<EventDto>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventDetailDto>> GetEventAsync(int id) => SendAsync<EventDetailDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<List<MyEventDto>>> GetMyEventsAsync() => SendAsync<List<MyEventDto>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<EventDto>> CreateEventAsync(EventFormModel m) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", Json(EventBody(m)));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventFormModel m) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", Json(EventBody(m)));

    public Task<ApiResult<object>> DeleteEventAsync(int id) => SendAsync<object>(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        SendAsync<EventDto>(HttpMethod.Post, $"api/events/{eventId}/banner", FileContent(file));

    public Task<ApiFile?> GetBannerAsync(int eventId) => GetFileAsync($"api/events/{eventId}/banner");

    public Task<ApiResult<WeatherDto>> GetWeatherAsync(int eventId) => SendAsync<WeatherDto>(HttpMethod.Get, $"api/events/{eventId}/weather");

    // ======================= Categories and routes =======================

    public Task<ApiResult<CategoryDto>> GetCategoryAsync(int id) => SendAsync<CategoryDto>(HttpMethod.Get, $"api/categories/{id}");

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryFormModel m) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", Json(CategoryBody(m)));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, CategoryFormModel m) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}", Json(CategoryBody(m)));

    public Task<ApiResult<object>> DeleteCategoryAsync(int id) => SendAsync<object>(HttpMethod.Delete, $"api/categories/{id}");

    public Task<ApiResult<RouteDto>> GetRouteAsync(int categoryId) => SendAsync<RouteDto>(HttpMethod.Get, $"api/categories/{categoryId}/route");

    public Task<ApiResult<RouteDto>> PutRouteAsync(int categoryId, RouteFormModel m) =>
        SendAsync<RouteDto>(HttpMethod.Put, $"api/categories/{categoryId}/route",
            Json(new { startPoint = m.StartPoint, endPoint = m.EndPoint, routeDescription = m.RouteDescription, mapUrl = m.MapUrl }));

    // ======================= Enrolments and results =======================

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, $"api/categories/{categoryId}/enrolments");

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/enrolments/me");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<object>> CancelEnrolmentAsync(int enrolmentId) =>
        SendAsync<object>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    public Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, TimeSpan finishTime, int? position) =>
        SendAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/results", Json(new { finishTime, position }));

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, TimeSpan finishTime, int? position) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", Json(new { finishTime, position }));

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() => SendAsync<List<ResultDto>>(HttpMethod.Get, "api/users/me/results");

    // ======================= Plumbing =======================

    private static object EventBody(EventFormModel m) => new
    {
        eventName = m.EventName,
        eventDate = DateOnly.FromDateTime(m.EventDate!.Value),
        location = m.Location,
        description = m.Description,
        eventType = m.EventType,
        distanceKm = m.DistanceKm
    };

    private static object CategoryBody(CategoryFormModel m) => new
    {
        categoryName = m.CategoryName,
        distanceKm = m.DistanceKm,
        maxParticipants = m.MaxParticipants,
        entryFee = m.EntryFee
    };

    private static HttpContent Json(object body) => JsonContent.Create(body, options: JsonOptions);

    private static HttpContent FileContent(IFormFile file)
    {
        var stream = new StreamContent(file.OpenReadStream());
        if (MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType))
            stream.Headers.ContentType = contentType;
        return new MultipartFormDataContent { { stream, "file", file.FileName } };
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        var cookie = Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    /// <summary>Remembers the API's session cookie (and any refreshed copy of it) inside the web session.</summary>
    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;

        foreach (var header in values)
        {
            if (!header.StartsWith(ApiCookieName, StringComparison.Ordinal)) continue;
            var pair = header.Split(';')[0];
            if (pair.Length > ApiCookieName.Length) // an empty value means "cookie deleted" (logout) - ignore it
                Session.SetString(SessionKeys.ApiCookie, pair);
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var request = NewRequest(method, url, content);
            using var response = await _http.SendAsync(request);
            CaptureCookie(response);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Error = await ReadErrorAsync(response)
                };
            }

            T? data = default;
            if (response.StatusCode != HttpStatusCode.NoContent && typeof(T) != typeof(object))
                data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);

            return new ApiResult<T> { Success = true, StatusCode = (int)response.StatusCode, Data = data };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ApiResult<T>
            {
                Success = false,
                StatusCode = 503,
                Error = "The RaceDay service is not responding right now. Please try again in a moment."
            };
        }
    }

    private async Task<ApiFile?> GetFileAsync(string url)
    {
        try
        {
            using var request = NewRequest(HttpMethod.Get, url);
            using var response = await _http.SendAsync(request);
            CaptureCookie(response);
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var type = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return new ApiFile(bytes, type);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Turns the API's error JSON into one short, readable sentence (never raw JSON or a stack trace).</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors.EnumerateObject()
                            .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                            .SelectMany(p => p.Value.EnumerateArray())
                            .Select(v => v.GetString() ?? string.Empty)
                            .Where(m => m.Length > 0)
                            .ToList();
                        if (messages.Count > 0) return string.Join(" ", messages);
                    }

                    if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(detail.GetString()))
                        return detail.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // not JSON - fall through to the friendly default
        }

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Some of the information you entered isn't valid. Please check it and try again.",
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.Conflict => "That conflicts with existing information.",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => "A service RaceDay depends on is unavailable right now. Please try again later.",
            _ => "Something went wrong. Please try again."
        };
    }
}

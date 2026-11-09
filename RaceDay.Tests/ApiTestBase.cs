using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Helpers shared by the API tests: sign up and log in users, and create events, categories and enrolments.</summary>
public abstract class ApiTestBase
{
    public const string TestPassword = "Passw0rd!123";

    protected ApiTestBase(RaceDayFactory factory)
    {
        this.Factory = factory;
    }

    protected RaceDayFactory Factory { get; }

    /// <summary>A client that keeps cookies, like a browser does, so the API session survives between calls.</summary>
    protected HttpClient NewClient() =>
        this.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>Registers a brand-new user (unique e-mail) and logs them in. Returns the logged-in client, e-mail and password.</summary>
    protected async Task<(HttpClient Client, string Email, string Password)> SignUp(string accountType)
    {
        var email = $"{accountType.ToLowerInvariant()}.{Guid.NewGuid():N}@test.raceday.co.za";

        var client = this.NewClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = $"Test {accountType}",
            email,
            password = TestPassword,
            accountType
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestPassword });
        login.EnsureSuccessStatusCode();

        return (client, email, TestPassword);
    }

    protected static async Task<EventResponse> CreateEvent(HttpClient organiser, string eventType = "Run")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = $"Test {eventType} {Guid.NewGuid():N}"[..30],
            description = "Created by a test.",
            eventDate = "2030-06-15",
            location = "Standerton",
            eventType,
            distanceKm = 10
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    protected static async Task<CategoryResponse> CreateCategory(HttpClient organiser, int eventId, int maxParticipants = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            categoryName = "Open",
            distanceKm = 10,
            entryFee = 150,
            maxParticipants
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    protected static async Task<EnrolmentResponse> Enrol(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }
}

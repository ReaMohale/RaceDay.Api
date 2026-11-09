using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Part 2: accounts, sessions, role and ownership rules, categories, enrolments and results.</summary>
public class Part2ApiTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public Part2ApiTests(RaceDayFactory factory) : base(factory) { }

    // ---------------------------------------------------------------- registration and login

    [Fact]
    public async Task Register_ValidAccount_Returns201WithoutAnyPassword()
    {
        var email = $"new.{Guid.NewGuid():N}@test.raceday.co.za";

        var response = await this.NewClient().PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "New Person",
            email,
            password = TestPassword,
            accountType = "Participant"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(email, body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var (_, email, _) = await this.SignUp("Participant");

        var response = await this.NewClient().PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Someone Else",
            email = email.ToUpperInvariant(),
            password = TestPassword,
            accountType = "Organiser"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Administrator", TestPassword)]   // not an allowed account type
    [InlineData("Participant", "short")]          // password under 8 characters
    public async Task Register_InvalidDetails_Returns400(string accountType, string password)
    {
        var response = await this.NewClient().PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Bad Input",
            email = $"bad.{Guid.NewGuid():N}@test.raceday.co.za",
            password,
            accountType
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Passwords_AreStoredHashed()
    {
        var (_, email, _) = await this.SignUp("Participant");

        using var scope = this.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.Participants.Single(p => p.Email == email.ToLowerInvariant()).PasswordHash;

        Assert.NotEqual(TestPassword, stored);
        Assert.DoesNotContain(TestPassword, stored);
        Assert.StartsWith("v1.", stored);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (_, email, _) = await this.SignUp("Participant");

        var response = await this.NewClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-Password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await this.NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@test.raceday.co.za", password = TestPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_StartsASession_AndLogoutEndsIt()
    {
        var (client, email, _) = await this.SignUp("Participant");

        var me = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me");
        Assert.Equal(email.ToLowerInvariant(), me!.Email);
        Assert.Equal("Participant", me.Role);

        var logout = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var after = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    // ---------------------------------------------------------------- roles and ownership

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_Returns401()
    {
        var response = await this.NewClient().GetAsync("/api/enrolments/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (participant, _, _) = await this.SignUp("Participant");

        var response = await participant.PostAsJsonAsync("/api/events", new
        {
            eventName = "Nope",
            eventDate = "2030-01-01",
            location = "X",
            eventType = "Run",
            distanceKm = 5
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEditOrDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await this.SignUp("Organiser");
        var (other, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(owner);

        var update = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            eventName = "Hijacked",
            eventDate = "2030-01-01",
            location = "X",
            eventType = "Run",
            distanceKm = 5
        });
        var delete = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    // ---------------------------------------------------------------- events and categories

    [Fact]
    public async Task CreateEvent_InvalidType_Returns400()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Bad type",
            eventDate = "2030-01-01",
            location = "X",
            eventType = "Swim",
            distanceKm = 5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingDate_Returns400()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "No date",
            location = "X",
            eventType = "Run",
            distanceKm = 5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Events_AreVisibleWithoutLogin_AndUnknownEventIs404()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(organiser);
        var anonymous = this.NewClient();

        var fetched = await anonymous.GetFromJsonAsync<EventResponse>($"/api/events/{ev.EventId}");
        var missing = await anonymous.GetAsync("/api/events/999999");

        Assert.Equal(ev.EventId, fetched!.EventId);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Category_CanBeAdded_ListedAndUpdated_ByTheOwner_ButNotByAnotherOrganiser()
    {
        var (owner, _, _) = await this.SignUp("Organiser");
        var (other, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(owner);
        var category = await CreateCategory(owner, ev.EventId, maxParticipants: 50);

        var listed = await this.NewClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");
        var update = await owner.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { categoryName = "Masters", distanceKm = 10, entryFee = 120, maxParticipants = 60 });
        var foreignAdd = await other.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { categoryName = "Sneaky", distanceKm = 5, entryFee = 0, maxParticipants = 10 });

        Assert.Contains(listed!, c => c.CategoryId == category.CategoryId);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Masters", (await update.Content.ReadFromJsonAsync<CategoryResponse>())!.CategoryName);
        Assert.Equal(HttpStatusCode.Forbidden, foreignAdd.StatusCode);
    }

    [Fact]
    public async Task Category_WithEnrolments_CannotBeDeleted_Returns409()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await Enrol(participant, category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------------------------------------------------------- enrolments

    [Fact]
    public async Task Enrol_TwiceInTheSameCategory_Returns409()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await Enrol(participant, category.CategoryId);

        var again = await participant.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_FullCategory_Returns400_AndSpotsTakenIsReported()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (first, _, _) = await this.SignUp("Participant");
        var (second, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId, maxParticipants: 1);
        await Enrol(first, category.CategoryId);

        var full = await second.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);
        var listed = await this.NewClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Equal(1, Assert.Single(listed!).SpotsTaken);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_ButNotSomeoneElses()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (owner, _, _) = await this.SignUp("Participant");
        var (other, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(owner, category.CategoryId);

        var foreign = await other.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var own = await owner.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
    }

    [Fact]
    public async Task Organiser_SeesEnrolmentsOfOwnEventOnly()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (other, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await Enrol(participant, category.CategoryId);

        var mine = await organiser.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventId}/enrolments");
        var foreign = await other.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Single(mine!);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    // ---------------------------------------------------------------- results

    [Fact]
    public async Task Result_CanBeCapturedOnce_ThenConflicts()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, category.CategoryId);

        var first = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new { finishTime = "00:47:31", position = 1 });
        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new { finishTime = "00:50:00", position = 2 });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Result_FinishTimeOf24HoursOrMore_Returns400()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, category.CategoryId);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new { finishTime = "1.00:00:00", position = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Result_IsVisibleToItsParticipant_ButNotToAnotherParticipant()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var (stranger, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, category.CategoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", new { finishTime = "00:47:31", position = 1 });

        var own = await participant.GetFromJsonAsync<ResultResponse>($"/api/enrolments/{enrolment.EnrolmentId}/results");
        var foreign = await stranger.GetAsync($"/api/enrolments/{enrolment.EnrolmentId}/results");

        Assert.Equal(new TimeSpan(0, 47, 31), own!.FinishTime);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }
}

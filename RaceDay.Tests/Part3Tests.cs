using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Part 3 API additions: event filters, organiser dashboard data, status flags and finisher counts.</summary>
public class Part3ApiTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public Part3ApiTests(RaceDayFactory factory) : base(factory) { }

    [Fact]
    public async Task GetEvents_FilterByType_ReturnsOnlyThatType()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var cycle = await CreateEvent(organiser, "Cycle");
        var run = await CreateEvent(organiser, "Run");

        var list = await this.Factory.CreateClient().GetFromJsonAsync<List<EventResponse>>("/api/events?type=Cycle");

        Assert.Contains(list!, e => e.EventId == cycle.EventId);
        Assert.DoesNotContain(list!, e => e.EventId == run.EventId);
        Assert.All(list!, e => Assert.Equal("Cycle", e.EventType));
    }

    [Fact]
    public async Task GetEvents_Search_MatchesEventName()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var unique = "Zebra" + Guid.NewGuid().ToString("N")[..8];
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = unique,
            eventDate = "2030-01-01",
            location = "Standerton",
            eventType = "Walk",
            distanceKm = 5
        });
        response.EnsureSuccessStatusCode();

        var list = await this.Factory.CreateClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?search={unique}");

        var match = Assert.Single(list!);
        Assert.Equal(unique, match.EventName);
    }

    [Fact]
    public async Task GetEvents_UpcomingOnly_HidesPastEvents()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var past = await organiser.PostAsJsonAsync("/api/events", new
        {
            eventName = "Old Race",
            eventDate = "2020-01-01",
            location = "X",
            eventType = "Run",
            distanceKm = 5
        });
        var pastEvent = (await past.Content.ReadFromJsonAsync<EventResponse>())!;

        var list = await this.Factory.CreateClient().GetFromJsonAsync<List<EventResponse>>("/api/events?upcomingOnly=true");

        Assert.DoesNotContain(list!, e => e.EventId == pastEvent.EventId);
    }

    [Fact]
    public async Task Organiser_MyEvents_ShowsCountsForOwnEventsOnly()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (otherOrganiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await CreateEvent(otherOrganiser);
        await Enrol(participant, category.CategoryId);

        var summaries = await organiser.GetFromJsonAsync<List<OrganiserEventSummary>>("/api/organisers/me/events");

        var mine = Assert.Single(summaries!);
        Assert.Equal(ev.EventId, mine.EventId);
        Assert.Equal(1, mine.CategoryCount);
        Assert.Equal(1, mine.EnrolmentCount);
        Assert.Equal(0, mine.ResultCount);
    }

    [Fact]
    public async Task Participant_CannotViewOrganiserDashboardData_Returns403()
    {
        var (participant, _, _) = await this.SignUp("Participant");
        var response = await participant.GetAsync("/api/organisers/me/events");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrolment_HasResultFlag_ChangesAfterResultIsCaptured()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, category.CategoryId);
        Assert.False(enrolment.HasResult);

        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", new { finishTime = "00:40:00", position = 1 });

        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/me");
        Assert.True(Assert.Single(mine!).HasResult);
    }

    [Fact]
    public async Task Result_IncludesTotalFinishersInTheCategory()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (first, _, _) = await this.SignUp("Participant");
        var (second, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var e1 = await Enrol(first, category.CategoryId);
        var e2 = await Enrol(second, category.CategoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{e1.EnrolmentId}/results", new { finishTime = "00:40:00", position = 1 });
        await organiser.PostAsJsonAsync($"/api/enrolments/{e2.EnrolmentId}/results", new { finishTime = "00:45:00", position = 2 });

        var results = await first.GetFromJsonAsync<List<ResultResponse>>("/api/participants/me/results");

        var result = Assert.Single(results!);
        Assert.Equal(2, result.TotalFinishers);
        Assert.Equal(1, result.Position);
    }

    [Fact]
    public async Task Organiser_CanListResultsForOwnEvent_AndOthersCannot()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (other, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, category.CategoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", new { finishTime = "00:50:00", position = 3 });

        var list = await organiser.GetFromJsonAsync<List<ResultResponse>>($"/api/events/{ev.EventId}/results");
        var forbidden = await other.GetAsync($"/api/events/{ev.EventId}/results");

        Assert.Single(list!);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Part 3: Azure Blob image uploads (banners and profile pictures).</summary>
public class ImageUploadTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public ImageUploadTests(RaceDayFactory factory) : base(factory) { }

    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };

    private static MultipartFormDataContent ImageForm(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "File", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_UrlIsSavedOnTheEvent()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(organiser);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("banner.png", PngBytes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Contains("/banners/", updated!.BannerUrl);

        var fetched = await organiser.GetFromJsonAsync<EventResponse>($"/api/events/{ev.EventId}");
        Assert.False(string.IsNullOrEmpty(fetched!.BannerUrl));
    }

    [Fact]
    public async Task UploadBanner_NotAnImage_Returns400()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(organiser);
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("this is just text, not an image");

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("banner.png", notAnImage));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WrongExtension_Returns400()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(organiser);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("banner.exe", PngBytes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var (organiser, _, _) = await this.SignUp("Organiser");
        var (participant, _, _) = await this.SignUp("Participant");
        var ev = await CreateEvent(organiser);

        var response = await participant.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("banner.png", PngBytes));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await this.SignUp("Organiser");
        var (other, _, _) = await this.SignUp("Organiser");
        var ev = await CreateEvent(owner);

        var response = await other.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("banner.png", PngBytes));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUploadBanner_Returns401()
    {
        var response = await this.Factory.CreateClient().PostAsync("/api/events/1/banner", ImageForm("banner.png", PngBytes));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task User_CanUploadProfilePicture_UrlAppearsOnProfile(string accountType)
    {
        var (client, _, _) = await this.SignUp(accountType);

        var upload = await client.PostAsync("/api/users/me/picture", ImageForm("me.png", PngBytes));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me");
        Assert.Contains("/profiles/", profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task UploadProfilePicture_WithoutLogin_Returns401()
    {
        var response = await this.Factory.CreateClient().PostAsync("/api/users/me/picture", ImageForm("me.png", PngBytes));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

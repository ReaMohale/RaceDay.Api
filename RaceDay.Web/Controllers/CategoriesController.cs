using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser page for adding, editing and removing the age or distance categories of an event.</summary>
[RequireLogin(Roles.Organiser)]
public class CategoriesController : RaceDayController
{
    public CategoriesController(IRaceDayApi api) : base(api)
    {
    }

    public async Task<IActionResult> Index(int eventId)
    {
        var page = await this.BuildPageAsync(eventId);
        if (page.Redirect is not null)
            return page.Redirect;

        return this.View(page.Model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel newCategory)
    {
        if (!this.ModelState.IsValid)
            return await this.ShowIndexWithErrorsAsync(newCategory);

        var result = await this.Api.CreateCategoryAsync(newCategory);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
        {
            this.ApplyApiErrors(result, newCategory);
            return await this.ShowIndexWithErrorsAsync(newCategory);
        }

        this.FlashSuccess($"Category \"{newCategory.CategoryName}\" was added.");
        return this.RedirectToAction(nameof(Index), new { eventId = newCategory.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        var page = await this.BuildPageAsync(eventId);
        if (page.Redirect is not null)
            return page.Redirect;

        var category = page.Model!.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (category is null)
            return this.NotFound();

        this.ViewData["EventName"] = page.Model.Event.EventName;
        return this.View(new CategoryFormViewModel
        {
            EventId = eventId,
            CategoryId = id,
            CategoryName = category.CategoryName,
            DistanceKm = category.DistanceKm,
            EntryFee = category.EntryFee,
            MaxParticipants = category.MaxParticipants
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int eventId, int id, CategoryFormViewModel model)
    {
        model.EventId = eventId;
        model.CategoryId = id;

        if (!this.ModelState.IsValid)
            return this.View(model);

        var result = await this.Api.UpdateCategoryAsync(id, model);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
        {
            this.ApplyApiErrors(result, model);
            return this.View(model);
        }

        this.FlashSuccess("The category was updated.");
        return this.RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await this.Api.DeleteCategoryAsync(id);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (result.Success)
            this.FlashSuccess("The category was deleted.");
        else
            this.FlashError(result.Message);

        return this.RedirectToAction(nameof(Index), new { eventId });
    }

    // ------------------------------------------------------------- helpers

    private async Task<IActionResult> ShowIndexWithErrorsAsync(CategoryFormViewModel posted)
    {
        var page = await this.BuildPageAsync(posted.EventId);
        if (page.Redirect is not null)
            return page.Redirect;

        page.Model!.NewCategory = posted;
        return this.View("Index", page.Model);
    }

    /// <summary>Loads the event and its categories, making sure the event belongs to the signed-in organiser.</summary>
    private async Task<(CategoriesViewModel? Model, IActionResult? Redirect)> BuildPageAsync(int eventId)
    {
        var eventResult = await this.Api.GetEventAsync(eventId);
        if (eventResult.StatusCode == StatusCodes.Status404NotFound)
            return (null, this.NotFound());

        var expired = this.RedirectIfSessionExpired(eventResult);
        if (expired is not null)
            return (null, expired);

        if (!eventResult.Success || eventResult.Data is null)
        {
            this.FlashError(eventResult.Message);
            return (null, this.RedirectToAction("Events", "Organiser"));
        }

        if (eventResult.Data.OrganiserId != this.CurrentUser!.UserId)
            return (null, this.RedirectToAction("AccessDenied", "Account"));

        var categories = await this.Api.GetCategoriesAsync(eventId);
        if (!categories.Success)
            this.ViewData["LoadError"] = categories.Message;

        var model = new CategoriesViewModel
        {
            Event = eventResult.Data,
            Categories = categories.Data ?? new List<CategoryItem>(),
            NewCategory = new CategoryFormViewModel { EventId = eventId }
        };
        return (model, null);
    }
}

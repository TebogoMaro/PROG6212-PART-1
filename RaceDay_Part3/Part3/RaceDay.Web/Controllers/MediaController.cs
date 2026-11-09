using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Serves images to the browser. The web app asks the API for the file (the API reads it from Azure Blob Storage) -
/// this app never talks to Azure itself.
/// </summary>
public class MediaController : AppController
{
    public MediaController(RaceDayApiClient api) : base(api) { }

    [HttpGet("/media/event/{id:int}")]
    public async Task<IActionResult> EventBanner(int id) => Serve(await Api.GetBannerAsync(id), "public, max-age=300");

    [HttpGet("/media/profile"), RoleAuthorize]
    public async Task<IActionResult> ProfilePicture() => Serve(await Api.GetPictureAsync(), "no-store");

    private IActionResult Serve(ApiFile? file, string cacheControl)
    {
        if (file is null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        Response.Headers.CacheControl = cacheControl;
        return File(file.Content, file.ContentType);
    }
}

using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    public AccountController(RaceDayApiClient api) : base(api) { }

    // ----------------------- Register -----------------------

    [HttpGet]
    public IActionResult Register()
    {
        if (CurrentUser is not null) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role != AppRoles.Organiser && model.Role != AppRoles.Participant)
            ModelState.AddModelError(nameof(model.Role), "Please choose Organiser or Participant.");
        if (model.Role == AppRoles.Organiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need the organiser registration code.");
        if (!ModelState.IsValid) return View(model);

        var result = await Api.RegisterAsync(model.FullName, model.Email, model.Password, model.Role,
            model.Role == AppRoles.Organiser ? model.OrganiserCode : null);

        if (!result.Success)
        {
            AddApiError(result);
            return View(model);
        }

        Success("Your account has been created. Please log in.");
        return RedirectToAction(nameof(Login));
    }

    // ----------------------- Login / Logout -----------------------

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (CurrentUser is not null) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        HttpContext.Session.Clear(); // start from a clean session
        var result = await Api.LoginAsync(model.Email, model.Password);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty,
                result.StatusCode == 401 ? "Incorrect email or password." : result.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        var user = result.Data;
        HttpContext.Session.SetObject(SessionKeys.User,
            new SessionUser(user.UserId, user.FullName, user.Email, user.Role, user.HasProfilePicture));

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return user.Role == AppRoles.Organiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await Api.LogoutAsync();
        HttpContext.Session.Clear();
        Success("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    // ----------------------- Profile -----------------------

    [HttpGet, RoleAuthorize]
    public async Task<IActionResult> Profile()
    {
        var result = await Api.GetMeAsync();
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success || result.Data is null)
        {
            Failure(result.Error ?? "Your profile could not be loaded.");
            return RedirectToAction("Index", "Home");
        }

        var user = result.Data;
        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            HasProfilePicture = user.HasProfilePicture
        });
    }

    [HttpPost, RoleAuthorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        model.Role = CurrentUser!.Role;
        model.HasProfilePicture = CurrentUser.HasProfilePicture;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.UpdateMeAsync(model.FullName, model.Email);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success || result.Data is null)
        {
            AddApiError(result);
            return View(model);
        }

        var user = result.Data;
        HttpContext.Session.SetObject(SessionKeys.User,
            new SessionUser(user.UserId, user.FullName, user.Email, user.Role, user.HasProfilePicture));

        Success("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, RoleAuthorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            Failure("Please choose a picture to upload.");
            return RedirectToAction(nameof(Profile));
        }

        var result = await Api.UploadPictureAsync(picture);
        var expired = ExpiredSession(result);
        if (expired is not null) return expired;

        if (!result.Success || result.Data is null)
        {
            Failure(result.Error ?? "The picture could not be uploaded.");
            return RedirectToAction(nameof(Profile));
        }

        var user = result.Data;
        HttpContext.Session.SetObject(SessionKeys.User,
            new SessionUser(user.UserId, user.FullName, user.Email, user.Role, user.HasProfilePicture));

        Success("Your profile picture has been updated.");
        return RedirectToAction(nameof(Profile));
    }
}

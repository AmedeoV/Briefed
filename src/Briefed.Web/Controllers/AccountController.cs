using Briefed.Core.Entities;
using Briefed.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Briefed.Infrastructure.Data;

namespace Briefed.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly BriefedDbContext _context;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<AccountController> logger,
        BriefedDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _context = context;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new User
        {
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Articles");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return RedirectToLocal(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult DeleteAccount()
    {
        return View();
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var feedCount = await _context.UserFeeds.CountAsync(uf => uf.UserId == user.Id);
        var savedArticlesCount = await _context.SavedArticles.CountAsync(sa => sa.UserId == user.Id);

        var model = new ProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt,
            FeedCount = feedCount,
            SavedArticlesCount = savedArticlesCount
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please fill in all fields correctly.";
            return RedirectToAction("Profile");
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            TempData["ErrorMessage"] = "New password and confirmation password do not match.";
            return RedirectToAction("Profile");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Your password has been changed successfully.";
            _logger.LogInformation("User {UserId} changed their password successfully.", user.Id);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            TempData["ErrorMessage"] = $"Failed to change password: {errors}";
            _logger.LogWarning("Failed to change password for user {UserId}: {Errors}", user.Id, errors);
        }

        return RedirectToAction("Profile");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMyAccount(string confirmPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        // Verify password before deletion
        var passwordCheck = await _userManager.CheckPasswordAsync(user, confirmPassword);
        if (!passwordCheck)
        {
            TempData["ErrorMessage"] = "Incorrect password. Account deletion cancelled.";
            return RedirectToAction("Profile");
        }

        try
        {
            // Delete user data
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                await _signInManager.SignOutAsync();
                TempData["SuccessMessage"] = "Your account has been deleted successfully.";
                _logger.LogInformation("User {UserId} ({Email}) deleted their account.", user.Id, user.Email);
                return RedirectToAction("Index", "Home");
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                TempData["ErrorMessage"] = $"Failed to delete account: {errors}";
                _logger.LogError("Failed to delete account for user {UserId}: {Errors}", user.Id, errors);
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while deleting your account. Please try again or contact support.";
            _logger.LogError(ex, "Error deleting account for user {UserId}", user.Id);
        }

        return RedirectToAction("Profile");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Articles");
    }
}

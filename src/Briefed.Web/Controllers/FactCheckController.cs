using Briefed.Core.Interfaces;
using Briefed.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Briefed.Web.Controllers;

[Authorize]
public class FactCheckController : Controller
{
    private readonly IFactCheckService _factCheckService;
    private readonly ILogger<FactCheckController> _logger;

    public FactCheckController(
        IFactCheckService factCheckService,
        ILogger<FactCheckController> logger)
    {
        _factCheckService = factCheckService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CheckClaim([FromBody] CheckClaimRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Claim))
        {
            return BadRequest(new { error = "Claim text is required" });
        }

        try
        {
            var result = await _factCheckService.CheckClaimAsync(request.Claim, request.LanguageCode);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking claim: {Claim}", request.Claim);
            return StatusCode(500, new { error = "An error occurred while checking the claim" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CheckArticle([FromBody] CheckArticleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ArticleText))
        {
            return BadRequest(new { error = "Article text is required" });
        }

        try
        {
            var results = await _factCheckService.CheckArticleAsync(request.ArticleText);
            return Json(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking article");
            return StatusCode(500, new { error = "An error occurred while checking the article" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        try
        {
            var isAvailable = await _factCheckService.IsAvailableAsync();
            return Json(new { available = isAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking fact-check service status");
            return Json(new { available = false, error = ex.Message });
        }
    }
}

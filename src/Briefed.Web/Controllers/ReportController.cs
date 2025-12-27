using Briefed.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Briefed.Web.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(IReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReportContent([FromBody] ReportContentRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request data." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "User not authenticated." });
            }

            var report = await _reportService.CreateReportAsync(
                userId,
                request.ArticleId,
                request.ContentType,
                request.Reason,
                request.AdditionalDetails
            );

            _logger.LogInformation(
                "Content report created. ID: {ReportId}, Type: {ContentType}, ArticleId: {ArticleId}, UserId: {UserId}",
                report.Id, request.ContentType, request.ArticleId, userId);

            return Ok(new { success = true, message = "Thank you for your report. We'll review it shortly." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content report");
            return StatusCode(500, new { success = false, message = "An error occurred while submitting your report." });
        }
    }
}

public class ReportContentRequest
{
    public int? ArticleId { get; set; }
    public string ContentType { get; set; } = string.Empty; // "Summary", "FactCheck"
    public string Reason { get; set; } = string.Empty;
    public string? AdditionalDetails { get; set; }
}

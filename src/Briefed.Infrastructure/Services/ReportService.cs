using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Briefed.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly BriefedDbContext _context;

    public ReportService(BriefedDbContext context)
    {
        _context = context;
    }

    public async Task<ContentReport> CreateReportAsync(string userId, int? articleId, string contentType, string reason, string? additionalDetails)
    {
        var report = new ContentReport
        {
            UserId = userId,
            ArticleId = articleId,
            ContentType = contentType,
            Reason = reason,
            AdditionalDetails = additionalDetails,
            ReportedAt = DateTime.UtcNow
        };

        _context.ContentReports.Add(report);
        await _context.SaveChangesAsync();

        return report;
    }

    public async Task<IEnumerable<ContentReport>> GetAllReportsAsync()
    {
        return await _context.ContentReports
            .Include(r => r.User)
            .Include(r => r.Article)
            .OrderByDescending(r => r.ReportedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContentReport>> GetUnresolvedReportsAsync()
    {
        return await _context.ContentReports
            .Include(r => r.User)
            .Include(r => r.Article)
            .Where(r => !r.IsResolved)
            .OrderByDescending(r => r.ReportedAt)
            .ToListAsync();
    }

    public async Task<ContentReport?> GetReportByIdAsync(int id)
    {
        return await _context.ContentReports
            .Include(r => r.User)
            .Include(r => r.Article)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task MarkAsResolvedAsync(int id, string resolvedBy, string? resolutionNotes)
    {
        var report = await _context.ContentReports.FindAsync(id);
        if (report == null)
        {
            throw new InvalidOperationException($"Report with ID {id} not found.");
        }

        report.IsResolved = true;
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedBy = resolvedBy;
        report.ResolutionNotes = resolutionNotes;

        await _context.SaveChangesAsync();
    }
}

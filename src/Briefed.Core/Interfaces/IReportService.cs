using Briefed.Core.Entities;

namespace Briefed.Core.Interfaces;

public interface IReportService
{
    Task<ContentReport> CreateReportAsync(string userId, int? articleId, string contentType, string reason, string? additionalDetails);
    Task<IEnumerable<ContentReport>> GetAllReportsAsync();
    Task<IEnumerable<ContentReport>> GetUnresolvedReportsAsync();
    Task<ContentReport?> GetReportByIdAsync(int id);
    Task MarkAsResolvedAsync(int id, string resolvedBy, string? resolutionNotes);
}

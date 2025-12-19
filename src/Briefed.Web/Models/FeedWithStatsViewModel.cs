using Briefed.Core.Entities;

namespace Briefed.Web.Models;

public class FeedWithStatsViewModel
{
    public Feed Feed { get; set; } = null!;
    public int TotalArticles { get; set; }
    public int UnreadArticles { get; set; }
}

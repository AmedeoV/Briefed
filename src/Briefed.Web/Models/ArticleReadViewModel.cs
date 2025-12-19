using Briefed.Core.Entities;

namespace Briefed.Web.Models;

public class ArticleReadViewModel
{
    public Article Article { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public bool IsSaved { get; set; }
}

namespace Briefed.Core.Entities;

public class DeletedArticle
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
}

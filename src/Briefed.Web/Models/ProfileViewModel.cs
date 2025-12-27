namespace Briefed.Web.Models;

public class ChangePasswordViewModel
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int FeedCount { get; set; }
    public int SavedArticlesCount { get; set; }
}

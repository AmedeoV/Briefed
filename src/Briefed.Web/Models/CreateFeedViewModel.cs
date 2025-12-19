using System.ComponentModel.DataAnnotations;

namespace Briefed.Web.Models;

public class CreateFeedViewModel
{
    [Required(ErrorMessage = "Feed URL is required")]
    [Url(ErrorMessage = "Please enter a valid URL")]
    [Display(Name = "Feed URL")]
    public string Url { get; set; } = string.Empty;

    [Display(Name = "Feed Title (Optional)")]
    [StringLength(500)]
    public string? Title { get; set; }
}

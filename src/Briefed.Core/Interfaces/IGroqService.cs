namespace Briefed.Core.Interfaces;

public interface IGroqService
{
    Task<string> GenerateSummaryAsync(string text, string summaryType = "comprehensive");
    Task<bool> IsAvailableAsync();
}

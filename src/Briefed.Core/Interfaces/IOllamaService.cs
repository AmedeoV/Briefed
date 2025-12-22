namespace Briefed.Core.Interfaces;

public interface IOllamaService
{
    Task<string> GenerateSummaryAsync(string text, string model = "qwen2.5:3b", string summaryType = "comprehensive");
    Task<bool> IsAvailableAsync();
}

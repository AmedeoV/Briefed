namespace Briefed.Core.Interfaces;

public interface IOllamaService
{
    Task<string> GenerateSummaryAsync(string text, string model = "llama3.2:3b");
    Task<bool> IsAvailableAsync();
}

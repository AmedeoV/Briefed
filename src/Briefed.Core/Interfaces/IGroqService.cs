namespace Briefed.Core.Interfaces;

public interface IGroqService
{
    Task<string> GenerateSummaryAsync(string text);
    Task<bool> IsAvailableAsync();
}

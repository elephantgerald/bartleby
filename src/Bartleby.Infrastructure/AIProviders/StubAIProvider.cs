using Bartleby.Core.Interfaces;
using Bartleby.Core.Models;

namespace Bartleby.Infrastructure.AIProviders;

/// <summary>
/// Stub AI provider that simulates work execution for testing.
/// </summary>
public class StubAIProvider : IAIProvider
{
    private readonly Random _random = new();

    public string Name => "Stub";

    public async Task<WorkExecutionResult> ExecuteWorkAsync(
        WorkItem workItem,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        // Simulate some work time
        await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 3)), cancellationToken);

        // Randomly decide outcome
        var outcome = _random.Next(100);

        if (outcome < 60) // 60% success
        {
            return new WorkExecutionResult
            {
                Success = true,
                Outcome = WorkExecutionOutcome.Completed,
                Summary = $"Successfully completed work on: {workItem.Title}",
                ModifiedFiles = ["src/Example.cs", "tests/ExampleTests.cs"],
                TokensUsed = _random.Next(1000, 5000)
            };
        }
        else if (outcome < 85) // 25% blocked
        {
            return new WorkExecutionResult
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Blocked,
                Summary = "Need more information to proceed",
                Questions =
                [
                    "What authentication provider should be used?",
                    "Should we support social login?"
                ],
                TokensUsed = _random.Next(500, 2000)
            };
        }
        else // 15% failed
        {
            return new WorkExecutionResult
            {
                Success = false,
                Outcome = WorkExecutionOutcome.Failed,
                ErrorMessage = "Simulated failure for testing purposes",
                TokensUsed = _random.Next(100, 500)
            };
        }
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

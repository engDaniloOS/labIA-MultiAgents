using Microsoft.Extensions.AI;

namespace Agent.Auto.Bank.Agents;

public interface IMessageAgent
{
    Task<string> RespondAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> RespondStreamingAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);
}

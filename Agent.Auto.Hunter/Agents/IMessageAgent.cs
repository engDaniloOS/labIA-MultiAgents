using Microsoft.Extensions.AI;

namespace Agent.Auto.Hunter.Agents;

public interface IMessageAgent
{
    Task<string> RespondAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> RespondStreamingAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);
}

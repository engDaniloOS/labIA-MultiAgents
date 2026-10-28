using Microsoft.Extensions.AI;

namespace Agent.Auto.Seller.Agents;

public interface IMessageAgent
{
    Task<string> RespondAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);
}

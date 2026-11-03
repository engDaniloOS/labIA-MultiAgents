using Microsoft.Extensions.AI;

namespace Agent.Auto.Seller.Agents;

public interface IMessageAgent
{
    Task<string> RespondAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);

    /// <summary>Igual a RespondAsync, mas entrega a resposta em pedaÃ§os de texto conforme o modelo a gera.</summary>
    IAsyncEnumerable<string> RespondStreamingAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default);
}

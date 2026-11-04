using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.A2A.Agents;

/// <summary>
/// Base dos agentes de conversa sobre um modelo de chat: o agente concreto só informa nome,
/// instruções e, se quiser, a temperatura.
/// </summary>
public abstract class ChatMessageAgent : IMessageAgent
{
    private readonly Lazy<AIAgent> _agent;

    // O cliente de chat chega "preguiçoso" para que a ausência da chave de API só falhe quando o
    // modelo for realmente usado, sem afetar a validação do JSON-RPC nem o Agent Card.
    // Nome, instruções e temperatura são lidos só nesse momento, quando a classe derivada já existe.
    protected ChatMessageAgent(Lazy<IChatClient> chatClient)
    {
        _agent = new Lazy<AIAgent>(() => chatClient.Value.AsAIAgent(new ChatClientAgentOptions
        {
            Name = Name,
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Temperature = Temperature
            }
        }), LazyThreadSafetyMode.PublicationOnly);
    }

    protected abstract string Name { get; }

    protected abstract string Instructions { get; }

    protected virtual float Temperature => 0.3f;

    public async Task<string> RespondAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        var response = await _agent.Value.RunAsync(history, cancellationToken: cancellationToken);
        return response.Text;
    }

    public async IAsyncEnumerable<string> RespondStreamingAsync(
        IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _agent.Value.RunStreamingAsync(history, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }
}

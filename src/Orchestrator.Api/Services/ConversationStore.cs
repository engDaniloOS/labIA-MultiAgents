using System.Collections.Concurrent;

namespace Agent.Auto.Orchestor.Services;

/// <summary>
/// Guarda, por conversa, qual agente respondeu por último. O roteador usa isso para manter o mesmo
/// agente em perguntas de continuação ("e o prazo?"). O histórico das mensagens em si fica em cada agente.
/// Em memória: some quando o orquestrador reinicia.
/// </summary>
public class ConversationStore
{
    private readonly ConcurrentDictionary<string, string> _lastAgent = new();

    public string? GetLastAgent(string conversationId)
        => _lastAgent.TryGetValue(conversationId, out var agent) ? agent : null;

    public void SetLastAgent(string conversationId, string agentKey)
        => _lastAgent[conversationId] = agentKey;
}

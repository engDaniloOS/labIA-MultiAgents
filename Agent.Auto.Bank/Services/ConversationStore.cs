using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Agent.Auto.Bank.Services;

public class ConversationStore
{
    private const int HistoryWindowSize = 10;

    private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();

    public IReadOnlyList<ChatMessage> AddUserMessage(string contextId, string text) => Add(contextId, ChatRole.User, text);

    public void AddAssistantMessage(string contextId, string text) => Add(contextId, ChatRole.Assistant, text);

    private IReadOnlyList<ChatMessage> Add(string contextId, ChatRole role, string text)
    {
        var messages = _conversations.GetOrAdd(contextId, _ => []);

        lock (messages)
        {
            messages.Add(new ChatMessage(role, text));

            var excess = messages.Count - HistoryWindowSize;
            if (excess > 0)
                messages.RemoveRange(0, excess);

            return [.. messages];
        }
    }
}

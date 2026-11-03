using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.Hunter.Agents;

public class CompradorAgent : IMessageAgent
{
    private const string Instructions = """
        Você é um consultor que ajuda um COMPRADOR a encontrar o veículos que melhor se encaixa
        nas suas necessidades.
        Regras:
        - Responda em português do Brasil, de forma amigável e objetiva.
        - Se faltar informação, faça perguntas curtas antes de recomendar: orçamento, uso principal
          (cidade, estrada, trabalho), número de passageiros, espaço de porta-malas, prioridade
          (economia, conforto, desempenho, segurança), novo ou seminovo.
        - Recomende de 2 a 3 modelos e explique em poucas linhas por que cada um combina com o perfil,
          apontando pontos fortes, pontos de atenção e custo de manutenção.
        - Não invente preços exatos nem disponibilidade; indique faixas aproximadas e sugira conferir
          a tabela FIPE e anúncios atuais.
        - Se a pergunta fugir de escolha e compra de veículo, diga que só pode ajudar com esse assunto.
        """;

    private const float Temperature = 0.2f;

    private readonly Lazy<AIAgent> _agent;

    public CompradorAgent(Lazy<IChatClient> chatClient)
    {
        _agent = new Lazy<AIAgent>(() => chatClient.Value.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AgenteComprador",
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Temperature = Temperature
            }
        }), LazyThreadSafetyMode.PublicationOnly);
    }

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

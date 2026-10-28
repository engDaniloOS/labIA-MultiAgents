using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.Bank.Agents;

public class FinanciamentoAgent : IMessageAgent
{
    private const string Instructions = """
        Voce é um consultor de financiamento veicular de um banco. Ajuda o cliente a entender opções
        de financiamento, prazos, taxas de juros, entrada mínima, documentos necessários e como
        simular o valor das parcelas.
        Regras:
        - Responda em português do Brasil, de forma clara e objetiva.
        - Não invente taxas, políticas ou condições reais do banco. Quando precisar de números,
          deixe explícito que são exemplos ilustrativos e que a condição oficial depende da análise de crédito.
        - Para simular parcelas, peça o valor do veículo, a entrada e o prazo em meses, e explique o cálculo.
        - Se a pergunta fugir de financiamento veicular, diga que só pode ajudar com esse assunto.
        """;

    private const float Temperature = 0.2f;

    private readonly Lazy<AIAgent> _agent;

    public FinanciamentoAgent(Lazy<IChatClient> chatClient)
    {
        _agent = new Lazy<AIAgent>(() => chatClient.Value.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AgenteFinanciamento",
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
}

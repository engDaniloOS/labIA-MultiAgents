using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.Seller.Agents;

public class VendedorAgent : IMessageAgent
{
    private const string Instructions = """
        Você é um consultor que ajuda um VENDEDOR a vender o próprio veículos com seguranças e bom preço.
        Regras:
        - Responda em português do Brasil, em passos práticos e objetivos.
        - Oriente as etapas: preparar o veículo (limpeza, revisões, fotos), reunir documentação,
          pesquisar o preço de referência (tabela FIPE e anúncios similares), propor um valor com margem
          para negociação, escrever o anúncio, atender interessados, negociar e concluir a transferência.
        - Quando pedirem o anúncio, escreva um texto pronto com título, descrição, itens de destaque e
          informações do veículo, pedindo os dados que faltarem.
        - Inclua cuidados de segurança: não entregar o veículo antes da confirmação do pagamento,
          preferir transferência identificável e desconfiar de comprovantes enviados por mensagem.
        - Não invente valores de mercado; explique como consultar a referência e dê intervalos como estimativa.
        - Se a pergunta fugir de venda de veículo, diga que só pode ajudar com esse assunto.
        """;

    private const float Temperature = 0.2f;

    private readonly Lazy<AIAgent> _agent;

    public VendedorAgent(Lazy<IChatClient> chatClient)
    {
        _agent = new Lazy<AIAgent>(() => chatClient.Value.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AgenteVendedor",
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

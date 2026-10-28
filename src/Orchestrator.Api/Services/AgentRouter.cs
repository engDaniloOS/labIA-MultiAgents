using System.Text;
using Agent.Auto.Orchestor.Configs;
using Agent.Auto.Orchestor.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Agent.Auto.Orchestor.Services;

public class RoutingException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Escolhe qual agente atende a mensagem. As opções vêm das skills dos Agent Cards, então um agente
/// novo entra no roteamento sem mudar este código. O LLM só devolve uma decisão estruturada; a
/// validação (agente existe? confiança suficiente?) é feita aqui, em código.
/// </summary>
public class AgentRouter(Lazy<IChatClient> chatClient, IOptions<RoutingOptions> options, ILogger<AgentRouter> logger)
{
    private const string DefaultClarification =
        "Não entendi bem o que você precisa. Você quer comprar um veículo, vender o seu ou saber sobre financiamento?";

    public async Task<RoutingResult> RouteAsync(
        string message,
        IReadOnlyList<AgentInfo> availableAgents,
        string? currentAgentKey,
        CancellationToken cancellationToken)
    {
        RoutingDecision decision;
        try
        {
            var agent = chatClient.Value.AsAIAgent(new ChatClientAgentOptions
            {
                Name = "Roteador",
                ChatOptions = new ChatOptions
                {
                    Instructions = BuildInstructions(availableAgents, currentAgentKey),
                    Temperature = 0f
                }
            });

            var response = await agent.RunAsync<RoutingDecision>(message, cancellationToken: cancellationToken);
            decision = response.Result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao rotear a mensagem");
            throw new RoutingException("Não foi possível decidir qual agente deve atender a mensagem.", ex);
        }

        var chosen = availableAgents.FirstOrDefault(a => string.Equals(a.Key, decision.Agent, StringComparison.OrdinalIgnoreCase));
        var confidence = Math.Clamp(decision.Confidence, 0, 1);

        logger.LogInformation("Roteamento: agente={Agent} confiança={Confidence:0.00} (limiar {Min})",
            decision.Agent, confidence, options.Value.MinConfidence);

        if (chosen is null || confidence < options.Value.MinConfidence)
        {
            var clarification = string.IsNullOrWhiteSpace(decision.Clarification) ? DefaultClarification : decision.Clarification;
            return new RoutingResult(null, confidence, clarification);
        }

        return new RoutingResult(chosen, confidence, null);
    }

    private static string BuildInstructions(IReadOnlyList<AgentInfo> agents, string? currentAgentKey)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é o roteador de um sistema de atendimento sobre veículos. Sua única tarefa é escolher qual agente deve atender a mensagem do usuário.");
        sb.AppendLine();
        sb.AppendLine("Agentes disponíveis:");

        foreach (var agent in agents)
        {
            sb.AppendLine($"- Chave: {agent.Key}");
            sb.AppendLine($"  Descrição: {agent.Card?.Description}");

            foreach (var skill in agent.Card?.Skills ?? [])
            {
                sb.AppendLine($"  Skill: {skill.Name} - {skill.Description}");
                sb.AppendLine($"  Temas: {string.Join(", ", skill.Tags)}");

                if (skill.Examples is { Count: > 0 })
                    sb.AppendLine($"  Exemplos: {string.Join(" | ", skill.Examples)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Regras:");
        sb.AppendLine("- Responda copiando a chave exata do agente, ou 'none' se nenhum se aplica.");
        sb.AppendLine("- Seja honesto na confiança: mensagens vagas, saudações ou que caibam em mais de um agente devem ter confiança baixa.");
        sb.AppendLine("- Quando a confiança for baixa ou o agente for 'none', preencha a clarificação com uma pergunta curta em português.");

        if (!string.IsNullOrWhiteSpace(currentAgentKey))
        {
            sb.AppendLine($"- A conversa está sendo atendida pelo agente '{currentAgentKey}'. Em perguntas de continuação, mantenha esse agente; troque somente se o assunto mudou claramente.");
        }

        return sb.ToString();
    }
}

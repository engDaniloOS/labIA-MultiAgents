using System.ComponentModel;

namespace Agent.Auto.Orchestor.Models;

/// <summary>Saída estruturada pedida ao LLM do roteador.</summary>
public sealed record RoutingDecision
{
    public const string NoAgent = "none";

    [Description("Chave exata do agente escolhido, copiada da lista. Use 'none' se nenhum agente se aplica ou se a mensagem for ambígua.")]
    public string Agent { get; init; } = NoAgent;

    [Description("Confiança na escolha, de 0 a 1. Use valores baixos quando a mensagem for vaga ou couber em mais de um agente.")]
    public double Confidence { get; init; }

    [Description("Quando Agent for 'none' ou a confiança for baixa: uma pergunta curta, em português, pedindo o que falta para escolher. Caso contrário, deixe vazio.")]
    public string? Clarification { get; init; }
}

/// <summary>Decisão já validada: o agente só vem preenchido se existir, estiver disponível e passar do limiar de confiança.</summary>
public sealed record RoutingResult(AgentInfo? Agent, double Confidence, string? Clarification);

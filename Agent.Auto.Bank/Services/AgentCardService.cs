namespace Agent.Auto.Bank.Services
{
    public class AgentCardService
    {
        public AgentCard GetAgentCard()
        {
            var skills = new List<Skill>
            {
                new() {
                    Id = "financeiro",
                    Name = "Agente de financiamento veicular",
                    Description = "Ajuda o cliente com informações sobre o financiamento veícular",
                    Examples =
                    [
                        "Quais as opções de financiamento?",
                        "Qual o prazo para quitação?",
                        "Qual a taxa de juros aplicada?",
                        "Quais são os documentos necessários para solicitar o financiamento?",
                        "Como posso simular o valor das parcelas do financiamento?",
                        "Qual o valor da entrada mínima exigida para o financiamento?"
                    ],
                    Tags = ["financeiro", "financiamento", "banco", "pagamento"]
                }
            };

            return new AgentCard
            {
                DefaultInputModes = ["text"],
                DefaultOutputModes = ["text"],
                Skills = skills,
                URL = "https://agent-auto-bank:8080"
            };
        }

        public class AgentCard
        {
            public List<string> DefaultInputModes { get; set; }
            public List<string> DefaultOutputModes { get; set; }
            public List<Skill> Skills { get; set; }
            public string URL { get; set; }
        }

        public class Skill
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public List<string> Examples { get; set; }
            public List<string> Tags { get; set; }
        }
    }
}

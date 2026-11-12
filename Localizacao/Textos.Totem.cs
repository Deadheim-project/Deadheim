namespace Deadheim
{
    internal static class TextosTotem
    {
        public static void Registrar()
        {
            L.Add("totem.nome", "Totem da Cidade",
                "City Totem");
            L.Add("totem.nome2", "Totem da Cidade",
                "City Totem");
            L.Add("totem.desligado_srv", "Desligado no servidor",
                "Turned off on the server");
            L.Add("totem.viajar", "\n[<color=yellow><b>$KEY_Use</b></color>] Viajar para a cidade (só ida)",
                "\n[<color=yellow><b>$KEY_Use</b></color>] Travel to the city (one way)");
            L.Add("totem.fora_ward", "Totem fora de ward: só funciona dentro de um ward.",
                "Totem outside a ward: it only works inside a ward.");
            L.Add("totem.ward_off", "Ward desligado: abasteca o ward desta base.",
                "Ward is off: fuel the ward of this base.");
            L.Add("totem.sem_acesso", "Sem acesso: só quem tem acesso ao ward desta base usa o totem.",
                "No access: only those with access to this base's ward can use the totem.");
            L.Add("totem.indisp", "Totem da Cidade indisponível.",
                "City Totem unavailable.");
            L.Add("totem.desl_srv", "Totem da Cidade desligado no servidor.",
                "City Totem turned off on the server.");
            L.Add("totem.cidade_indisp", "Cidade indisponível: o admin ainda não definiu a chegada.",
                "City unavailable: the admin has not set the arrival point yet.");
            L.Add("totem.carga", "Carga pesada: carregando minério ou metal você não viaja pelo totem.",
                "Heavy load: you cannot travel by totem while carrying ore or metal.");
            L.Add("totem.nao_saiu_t", "O teleporte não saiu",
                "The teleport did not go through");
            L.Add("totem.tente", "Tente de novo em instantes.",
                "Try again in a moment.");
            L.Add("totem.indo", "Indo para a cidade...",
                "Heading to the city...");
            L.Add("totem.coloque_t", "Coloque dentro de um ward",
                "Place it inside a ward");
            L.Add("totem.coloque_d", "O totem fica em ward seu ou de alguém da sua guilda.",
                "The totem goes in your ward or one from your guild.");
            L.Add("totem.conferindo", "Conferindo seus totens",
                "Checking your totems");
            L.Add("totem.ja_tem", "Você já tem um Totem da Cidade",
                "You already have a City Totem");
            L.Add("totem.remova_antigo", "Remova o antigo primeiro.",
                "Remove the old one first.");
            L.Add("totem.limite", "Limite de totens atingido ({0})",
                "Totem limit reached ({0})");
            L.Add("totem.remova_um", "Remova um para colocar outro.",
                "Remove one to place another.");
            L.Add("totem.chegada_gravada", "Chegada do totem gravada",
                "Totem arrival saved");
            L.Add("totem.desc_peca", "Leva você, só de ida, à cidade. Fica dentro de um ward seu e só quem tem acesso ao ward usa. Para voltar, use a pedra de retorno ou o /retreat.",
                "Takes you, one way, to the city. It sits inside your ward and only those with ward access can use it. To come back, use the return stone or /retreat.");
        }
    }
}

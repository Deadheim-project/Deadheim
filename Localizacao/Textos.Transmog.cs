namespace Deadheim
{
    internal static class TextosTransmog
    {
        public static void Registrar()
        {
            L.Add("transmog.npc_only", "Fale com o Alfaiate para trocar a aparência (/transmog off volta ao visual real).",
                "Talk to the Tailor to change your appearance (/transmog off returns to your real look).");
            L.Add("transmog.nao_perm", "Item não permitido para transmog.",
                "Item not allowed for transmog.");
            L.Add("transmog.mesmo_slot", "A aparência precisa ser do mesmo slot.",
                "The appearance must be for the same slot.");
            L.Add("transmog.mesma_classe", "A aparência precisa ser da mesma classe de peso.",
                "The appearance must be of the same weight class.");
            L.Add("transmog.so_aesir", "Transmog é exclusivo Aesir (VIP).",
                "Transmog is Aesir (VIP) exclusive.");
            L.Add("transmog.nao_vestiu", "Você ainda não vestiu essa peça (desbloqueia ao equipar).",
                "You have not worn that piece yet (it unlocks when equipped).");
            L.Add("transmog.limite", "Limite de trocas ativas atingido.",
                "Active swap limit reached.");
            L.Add("transmog.desativado", "Transmog desativado.",
                "Transmog is disabled.");
            L.Add("transmog.uso", "Uso: /transmog <real> <aparência> | /transmog off <real|*>",
                "Usage: /transmog <real> <appearance> | /transmog off <real|*>");
            L.Add("transmog.ui_ajuda", "Esquerda: a peça que você usa. Direita: a aparência que os outros veem. Desbloqueia ao equipar a peça.",
                "Left: the piece you wear. Right: the look others see. Unlocks when you equip the piece.");
            L.Add("transmog.peca_real", "Peça real",
                "Real piece");
            L.Add("transmog.aparencia_esc", "Aparência (escolha uma peça)",
                "Appearance (pick a piece)");
            L.Add("transmog.aparencia_para", "Aparência para {0}",
                "Appearance for {0}");
        }
    }
}

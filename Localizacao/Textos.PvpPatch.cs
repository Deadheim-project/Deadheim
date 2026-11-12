namespace Deadheim
{
    internal static class TextosPvpPatch
    {
        public static void Registrar()
        {
            L.Add("pvppatch.carga_t", "Carga perdida",
                "Cargo lost");
            L.Add("pvppatch.itens_chao", "{0} item(ns) caíram no chão.",
                "{0} item(s) dropped on the ground.");
            L.Add("pvppatch.moedas_t", "Moedas perdidas",
                "Coins lost");
            L.Add("pvppatch.moedas_chao", "{0} moedas caíram no chão.",
                "{0} coins dropped on the ground.");
            L.Add("pvppatch.pk_itens", "PK: itens perdidos",
                "PK: items lost");
            L.Add("pvppatch.sem_perda", "Sem perda de skill",
                "No skill loss");
            L.Add("pvppatch.skills_intactas", "Nesta morte suas skills ficam intactas.",
                "Your skills stay intact on this death.");
            L.Add("pvppatch.prot_treinador", "Proteção do Treinador",
                "Trainer protection");
            L.Add("pvppatch.perda_red", "Perda de skill reduzida em {0}%.",
                "Skill loss reduced by {0}%.");
            L.Add("pvppatch.pk_maior", "PK: perda de skill maior",
                "PK: bigger skill loss");
            L.Add("pvppatch.perdeu_skill", "Você perdeu skill x{0} nesta morte.",
                "You lost skill x{0} on this death.");
            L.Add("pvppatch.s_cacado", " <color=#ff8c00>[CAÇADO]</color>",
                " <color=#ff8c00>[HUNTED]</color>");
            L.Add("pvppatch.s_pkperm", " <color=#ff3030>[PK PERMANENTE x{0}]</color>",
                " <color=#ff3030>[PERMANENT PK x{0}]</color>");
            L.Add("pvppatch.s_agressor", " <color=#ff7a3d>[AGRESSOR]</color>",
                " <color=#ff7a3d>[AGGRESSOR]</color>");
            L.Add("pvppatch.s_imune", " <color=#7fd4ff>[IMUNE]</color>",
                " <color=#7fd4ff>[IMMUNE]</color>");
            L.Add("pvppatch.s_seguro", " <color=#7CFC00>[SEGURO]</color>",
                " <color=#7CFC00>[SAFE]</color>");
            L.Add("pvppatch.s_faixa", " <color=#a0a0a0>[outra faixa]</color>",
                " <color=#a0a0a0>[other bracket]</color>");
            L.Add("pvppatch.tumba_de", "Tumba de {0}",
                "Tombstone of {0}");
            L.Add("pvppatch.so_dono", "Só o dono abre esta tumba.",
                "Only the owner can open this tombstone.");
            L.Add("pvppatch.aberta", "\nAberta para todos",
                "\nOpen to everyone");
            L.Add("pvppatch.trancada", "\nTrancada: só o dono abre",
                "\nLocked: only the owner can open");
            L.Add("pvppatch.abre_todos", "\n<color=#ffd24a>Abre para todos em {0}</color>",
                "\n<color=#ffd24a>Opens to everyone in {0}</color>");
            L.Add("pvppatch.tp_cacado", "Caçado: não dá para teleportar.",
                "Hunted: you cannot teleport.");
            L.Add("pvppatch.tp_combate", "Em combate: teleporte liberado em {0}s.",
                "In combat: teleport available in {0}s.");
        }
    }
}

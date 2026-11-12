namespace Deadheim
{
    internal static class TextosForja
    {
        public static void Registrar()
        {
            L.Add("forja.teto", "A Forja de Potencial não passa do nível {0}.",
                "The Potential Forge does not go past level {0}.");
            L.Add("forja.faltam_msg", "Faltam garantias: o nível {0} pede {1}, você tem {2}. Para arriscar sem, desligue UsarGarantia em ESC > Deadheim.",
                "Missing guarantees: level {0} needs {1}, you have {2}. To risk it without, turn off UsarGarantia in ESC > Deadheim.");
            L.Add("forja.usada", "{0}: {1} usada(s) no nível {2}. Restam {3}.",
                "{0}: {1} used at level {2}. {3} left.");
            L.Add("forja.nivel_max", "Nível máximo da Forja de Potencial: {0}",
                "Potential Forge maximum level: {0}");
            L.Add("forja.chance", "<color=#9be564>{0}: {1} de sucesso</color> (usa {2}, tem {3})",
                "<color=#9be564>{0}: {1} success</color> (uses {2}, have {3})");
            L.Add("forja.refinar_gar", "Refinar com garantia",
                "Refine with guarantee");
            L.Add("forja.faltam_ui", "<color=#f0a040>Faltam garantias: o nível {0} pede {1}, você tem {2}</color>",
                "<color=#f0a040>Missing guarantees: level {0} needs {1}, you have {2}</color>");
        }
    }
}

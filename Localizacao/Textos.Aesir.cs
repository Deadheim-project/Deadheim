namespace Deadheim
{
    /// <summary>Textos dos modulos Aesir (Retreat, Pull, Plantio em area, Teclas) e Plantas.</summary>
    internal static class TextosAesir
    {
        public static void Registrar()
        {
            // Retreat
            L.Add("retreat.so_aesir", "só Aesir", "Aesir only");
            L.Add("retreat.desligado", "desligado", "off");
            L.Add("retreat.definir", "\n[<color=yellow><b>{0}</b></color>] definir: Hearthstone ({1}) · Retreat 1 ({2}) · Retreat 2 ({3})",
                "\n[<color=yellow><b>{0}</b></color>] set: Hearthstone ({1}) · Retreat 1 ({2}) · Retreat 2 ({3})");
            L.Add("retreat.voltar", "Voltar ao ponto de retreat (de qualquer lugar)", "Return to the retreat point (from anywhere)");
            L.Add("retreat.voltar2", "Voltar ao segundo ponto", "Return to the second point");
            L.Add("retreat.definido", "definido", "set");
            L.Add("retreat.nao", "não", "no");
            L.Add("retreat.nao_saiu", "O teleporte não saiu agora; tente de novo em instantes.", "The teleport did not go through; try again in a moment.");
            // Plantas (janela)
            L.Add("plantas.salvar_titulo", "Salvar as peças que você construiu perto de você:", "Save the pieces you built near you:");
            L.Add("plantas.nome", "Nome", "Name");
            L.Add("plantas.raio", "Raio: {0} m", "Radius: {0} m");
            L.Add("plantas.salvar", "Salvar planta aqui", "Save blueprint here");
            L.Add("plantas.ajuda", "Colocar: olhe para o chão onde a planta deve ficar. Cada peça gasta material e entra numa fila de 2 por segundo; ande até a obra se ela for grande. Não vale em combate, zona de raid nem ward alheio.",
                "Place: look at the ground where the blueprint should go. Each piece uses materials and is queued at 2 per second; walk to the build if it is large. Not allowed in combat, raid zones or other players' wards.");
            L.Add("plantas.cancelar", "Cancelar colocação", "Cancel placement");
            L.Add("plantas.fechar", "Fechar", "Close");
            L.Add("plantas.falta_material", "Falta material para {0}. Pegue mais ou cancele.", "Missing material for {0}. Get more or cancel.");
            // Pull
            L.Add("pull.fogueira", "Aesir: {0}x {1} do baú para a fogueira.", "Aesir: {0}x {1} from the chest to the fire.");
            L.Add("pull.bicho", "Aesir: {0} do baú para {1}.", "Aesir: {0} from the chest to {1}.");
            // Plantio em area
            L.Add("plantio.resultado", "Plantio em área: +{0} {1}", "Area planting: +{0} {1}");
            L.Add("plantio.sem_espaco", ", {0} sem espaço ou terra arada", ", {0} without space or cultivated soil");
            L.Add("plantio.barradas", ", {0} barrada(s)", ", {0} blocked");
            // Teclas / cosmeticos
            L.Add("teclas.abra_bau", "Abra o baú para ordenar.", "Open the chest to sort it.");
            L.Add("cosmeticos.ficha_antiga", "Esta ficha foi descontinuada. Os pets agora são itens individuais na Loja Deadcoins.",
                "This token was discontinued. Pets are now individual items in the Deadcoins Shop.");
        }
    }
}

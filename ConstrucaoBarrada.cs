using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Peca barrada nao cobra material (27/10/2026). No jogo 1.0 o TryPlacePiece chama o PlacePiece e
    /// devolve true sempre, e o UpdatePlacement cobra os materiais logo depois: um prefixo que barrava o
    /// PlacePiece (limite e distancia de ward, Territorio, Totem da Cidade, bau Aesir, RaidWard e zona
    /// de raid do RaidSystem) deixava o jogador sem a peca e sem o material.
    /// O ultimo prefixo do PlacePiece anota se algum anterior barrou (__runOriginal: no HarmonyX todos
    /// os prefixos rodam, o false so entra nesse valor) e o TryPlacePiece passa a devolver false, que o
    /// UpdatePlacement le como "nao colocou". Vale para qualquer mod que barre o PlacePiece.
    /// Quem chama o PlacePiece direto e cobra por conta propria (Plantas) le <see cref="Barrada"/>.
    /// </summary>
    public static class ConstrucaoBarrada
    {
        /// <summary>O ultimo PlacePiece foi barrado por algum prefixo.</summary>
        public static bool Barrada { get; private set; }

        /// <summary>Time.time da ultima peca que anotou a colocacao (<see cref="AoColocar"/>).</summary>
        public static float UltimaColocacao { get; private set; } = -999f;

        private static readonly List<Action> _aoColocar = new List<Action>();

        /// <summary>
        /// Para o prefixo que deixa a peca passar mas anota a colocacao (contagem local do limite,
        /// pino no mapa): roda so se nenhum prefixo deste PlacePiece barrar. Somar direto no prefixo
        /// contava a peca que um patch seguinte barrava, e o limite recusava a proxima de verdade.
        /// </summary>
        public static void AoColocar(Action acao)
        {
            if (acao != null) _aoColocar.Add(acao);
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        private static class LimparPatch
        {
            // Antes de todos: o que um PlacePiece anterior deixou (prefixo que estourou) nao vale para este.
            [HarmonyPriority(10000)]
            private static void Prefix() => _aoColocar.Clear();
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        private static class AnotarPatch
        {
            // Depois de todos os outros prefixos (o do bau Aesir ja e Priority.Last, que vale 0).
            [HarmonyPriority(-1000)]
            private static void Prefix(bool __runOriginal)
            {
                Barrada = !__runOriginal;
                if (Barrada || _aoColocar.Count == 0)
                {
                    _aoColocar.Clear();
                    return;
                }

                Action[] acoes = _aoColocar.ToArray();
                _aoColocar.Clear();
                UltimaColocacao = Time.time;
                foreach (Action acao in acoes)
                {
                    try { acao(); }
                    catch (Exception ex) { Debug.LogWarning("[Deadheim] Anotacao de colocacao falhou: " + ex.Message); }
                }
            }
        }

        [HarmonyPatch(typeof(Player), "TryPlacePiece")]
        private static class TryPlacePiecePatch
        {
            // O TryPlacePiece pode sair antes do PlacePiece (posicao invalida): sem isto valeria a anotacao anterior.
            private static void Prefix() => Barrada = false;

            private static void Postfix(ref bool __result)
            {
                if (Barrada) __result = false;
            }
        }
    }
}

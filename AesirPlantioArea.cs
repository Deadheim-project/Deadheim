using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Plantio em area (02/11/2026): com o ancinho, Shift ao plantar uma muda planta uma grade
    /// AesirPlantioAreaLado x AesirPlantioAreaLado dela em volta, alinhada com o jogador, no
    /// espacamento em que cada uma cresce (o dobro do m_growRadius da Plant). Par do Shift+E da
    /// colheita em area (Aesir 5). Quem usa e o AesirPlantioArea: SoAesir (padrao), Todos ou Desligado.
    ///
    /// Cada muda extra passa pelas mesmas travas de uma muda posta a mao -- terra arada, bioma,
    /// ward (PrivateArea), zona sem construcao, zona de raid -- e por Player.PlacePiece, onde os
    /// patches do Deadheim e do RaidSystem ainda podem barrar (ConstrucaoBarrada). Cobra a semente,
    /// o vigor e o desgaste do ancinho de cada uma, como o jogo cobra a primeira. Para quando
    /// acaba a semente ou o vigor. Os pontos sem espaco para crescer ficam vazios.
    /// </summary>
    [HarmonyPatch]
    internal static class AesirPlantioArea
    {
        private const int Item = 5;

        private static readonly AccessTools.FieldRef<Player, GameObject> Ghost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");
        private static readonly Func<Humanoid, ItemDrop.ItemData> RightItem =
            AccessTools.MethodDelegate<Func<Humanoid, ItemDrop.ItemData>>(AccessTools.Method(typeof(Humanoid), "GetRightItem"));
        private static readonly Func<Player, float> BuildStamina =
            AccessTools.MethodDelegate<Func<Player, float>>(AccessTools.Method(typeof(Player), "GetBuildStamina"));
        private static readonly Func<Player, ItemDrop.ItemData, float> PlaceDurability =
            AccessTools.MethodDelegate<Func<Player, ItemDrop.ItemData, float>>(AccessTools.Method(typeof(Player), "GetPlaceDurability"));

        private static int _spaceMask;

        // A grade sai no quadro seguinte: o jogo so cobra a semente da primeira muda depois que
        // o TryPlacePiece volta, e conferir o estoque antes disso contaria uma semente a mais.
        private static Piece _peca;
        private static Vector3 _centro;
        private static float _yaw;
        private static int _quadro = -1;

        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static class TryPlacePiecePatch
        {
            private static void Postfix(Player __instance, Piece piece, bool __result)
            {
                try { Anotar(__instance, piece, __result); }
                catch (Exception e) { AesirLog.Erro(Item, e); }
            }
        }

        private static bool ShiftApertado() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static void Anotar(Player player, Piece piece, bool colocou)
        {
            if (!colocou || ConstrucaoBarrada.Barrada || player != Player.m_localPlayer || piece == null) return;
            if (piece.GetComponent<Plant>() == null || !ShiftApertado()) return;
            var modo = AesirConfig.AesirPlantioArea?.Value ?? AesirConfig.ModoPlantioArea.Desligado;
            if (modo == AesirConfig.ModoPlantioArea.Desligado)
            {
                AesirLog.D(Item, "Shift ao plantar: negado (AesirPlantioArea=Desligado).");
                return;
            }
            if (modo == AesirConfig.ModoPlantioArea.SoAesir && !VipList.VipListApi.IsLocalPlayerVip())
            {
                AesirLog.D(Item, "Shift ao plantar: negado (nao e VIP, AesirPlantioArea=SoAesir).");
                return;
            }
            GameObject ghost = Ghost(player);
            if (ghost == null) return;
            _peca = piece;
            _centro = ghost.transform.position;
            _yaw = player.transform.eulerAngles.y;
            _quadro = Time.frameCount;
        }

        /// <summary>Do Plugin.Update: planta a grade anotada, um quadro depois da primeira muda.</summary>
        internal static void Tick()
        {
            if (_peca == null || Time.frameCount <= _quadro) return;
            Piece peca = _peca;
            _peca = null;
            try { Plantar(Player.m_localPlayer, peca, _centro, _yaw); }
            catch (Exception e) { AesirLog.Erro(Item, e); }
        }

        private static void Plantar(Player player, Piece piece, Vector3 centro, float yaw)
        {
            if (player == null || player.IsDead()) return;
            Plant plant = piece.GetComponent<Plant>();
            ItemDrop.ItemData ancinho = RightItem(player);
            if (plant == null || ancinho == null || ZoneSystem.instance == null) return;

            int lado = Mathf.Clamp(AesirConfig.AesirPlantioAreaLado?.Value ?? 5, 2, 9);
            float passo = Mathf.Max(0.5f, plant.m_growRadius * 2f + 0.05f);
            Quaternion giro = Quaternion.Euler(0f, yaw, 0f);
            Vector3 direita = giro * Vector3.right, frente = giro * Vector3.forward;
            float meio = (lado - 1) / 2f;
            bool semCusto = Priv.NoPlacementCost(player);
            bool gratis = semCusto || ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey());

            int plantadas = 0, semEspaco = 0, barradas = 0;
            string parou = null;
            for (int i = 0; i < lado && parou == null; i++)
            {
                for (int j = 0; j < lado; j++)
                {
                    // A muda que o jogador pos fica no meio (lado impar) ou num dos quatro do meio.
                    float dx = (i - meio) * passo, dz = (j - meio) * passo;
                    if (Mathf.Abs(dx) < passo * 0.5f && Mathf.Abs(dz) < passo * 0.5f) continue;

                    if (!semCusto && !player.HaveRequirements(piece, Player.RequirementMode.CanBuild)) { parou = "acabou a semente"; break; }
                    if (!player.HaveStamina(ancinho.m_shared.m_attack.m_attackStamina)) { parou = "acabou o vigor"; break; }

                    Vector3 p = centro + direita * dx + frente * dz;
                    p.y = ZoneSystem.instance.GetGroundHeight(p);
                    if (!PodePlantar(piece, plant, p)) { semEspaco++; continue; }

                    player.PlacePiece(piece, p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), doAttack: false);
                    if (ConstrucaoBarrada.Barrada) { barradas++; continue; }
                    if (!gratis) player.ConsumeResources(piece.m_resources, 0);
                    player.UseStamina(BuildStamina(player));
                    if (ancinho.m_shared.m_useDurability)
                        ancinho.m_durability -= PlaceDurability(player, ancinho) * Game.m_durabilityRate;
                    plantadas++;
                }
            }

            if (plantadas > 0) player.RaiseSkill(Skills.SkillType.Farming, plantadas);
            string nome = Localization.instance != null ? Localization.instance.Localize(piece.m_name) : piece.m_name;
            string texto = $"Plantio em área: +{plantadas} {nome}" +
                           (semEspaco > 0 ? $", {semEspaco} sem espaço ou terra arada" : "") +
                           (barradas > 0 ? $", {barradas} barrada(s)" : "") +
                           (parou != null ? $" ({parou})" : "");
            player.Message(MessageHud.MessageType.TopLeft, texto);
            AesirLog.D(Item, texto + $" [grade {lado}x{lado}, passo {passo:0.00} m]");
        }

        /// <summary>As travas de uma muda posta a mao (Player.UpdatePlacementGhost) e o espaco que
        /// a Plant pede para crescer (Plant.HaveGrowSpace).</summary>
        private static bool PodePlantar(Piece piece, Plant plant, Vector3 p)
        {
            Heightmap heightmap = Heightmap.FindHeightmap(p);
            if (piece.m_cultivatedGroundOnly && (heightmap == null || !heightmap.IsCultivated(p))) return false;
            if ((piece.m_groundOnly || piece.m_groundPiece) && heightmap == null) return false;
            if (piece.m_onlyInBiome != Heightmap.Biome.None && (Heightmap.FindBiome(p) & piece.m_onlyInBiome) == 0) return false;
            if (Location.IsInsideNoBuildLocation(p) || !PrivateArea.CheckAccess(p, 0f, false, false)) return false;

            if (_spaceMask == 0) _spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
            foreach (Collider col in Physics.OverlapSphere(p, plant.m_growRadius, _spaceMask))
            {
                Plant outra = col.GetComponentInParent<Plant>();
                // Uma muda morta ou murcha nao impede (o jogo tambem ignora); o resto sim.
                if (outra == null || outra.GetStatus() == Plant.Status.Healthy) return false;
            }
            return true;
        }
    }
}

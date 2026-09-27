using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Cla embutido, no lugar dos mods Guilds e Groups. O servidor e o dono da lista
    /// (PvpStore) e empurra para todos um diretorio playerId -> cla sempre que ela muda.
    /// Com o diretorio em maos, qualquer cliente responde "sao do mesmo cla?" sem
    /// perguntar a ninguem: e o que as regras de fogo amigo, a tumba e os wards precisam.
    /// </summary>
    internal static class Clans
    {
        private const int MinNameLength = 3;
        private const int MaxNameLength = 20;
        private const double InviteSeconds = 120d;

        // -------------------------------------------------------------------- cliente

        private static readonly Dictionary<long, string> _directory = new Dictionary<long, string>();

        private struct MatePosition
        {
            public string Name;
            public ZDOID Character;
            public Vector3 Position;
        }

        private static readonly List<MatePosition> _mates = new List<MatePosition>();
        private static float _matesReceivedAt = -999f;

        public static string ClanOf(long playerId)
            => playerId != 0L && _directory.TryGetValue(playerId, out string clan) ? clan : null;

        public static string OwnClan
            => Player.m_localPlayer != null ? ClanOf(Player.m_localPlayer.GetPlayerID()) : null;

        public static bool SameClan(long a, long b)
        {
            if (a == 0L || b == 0L || a == b) return false;
            string clanA = ClanOf(a);
            return clanA != null && string.Equals(clanA, ClanOf(b), StringComparison.OrdinalIgnoreCase);
        }

        public static void ClearClient()
        {
            _directory.Clear();
            _mates.Clear();
        }

        public static void ApplyDirectory(ZPackage pkg)
        {
            _directory.Clear();
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                long id = pkg.ReadLong();
                string clan = pkg.ReadString();
                _directory[id] = clan;
            }
        }

        public static void ApplyPositions(ZPackage pkg)
        {
            _mates.Clear();
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
                _mates.Add(new MatePosition { Name = pkg.ReadString(), Character = pkg.ReadZDOID(), Position = pkg.ReadVector3() });
            _matesReceivedAt = Time.time;
        }

        /// <summary>
        /// Membros do cla entram na lista de "jogadores com posicao publica" que o mapa do
        /// vanilla ja desenha. Sem marcador proprio, sem textura nova.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.GetOtherPublicPlayers))]
        private static class MatesOnMapPatch
        {
            private static void Postfix(List<ZNet.PlayerInfo> playerList)
            {
                if (playerList == null || _mates.Count == 0) return;
                if (!PvpConfig.Active || !PvpConfig.ClanEnabled.Value || !PvpConfig.ClanShowOnMap.Value) return;
                if (Time.time - _matesReceivedAt > 10f) return;

                foreach (MatePosition mate in _mates)
                {
                    int existing = playerList.FindIndex(p => p.m_characterID == mate.Character);
                    ZNet.PlayerInfo info = existing >= 0 ? playerList[existing] : new ZNet.PlayerInfo
                    {
                        m_name = mate.Name,
                        m_characterID = mate.Character,
                    };
                    info.m_publicPosition = true;
                    info.m_position = mate.Position;
                    if (existing >= 0) playerList[existing] = info;
                    else playerList.Add(info);
                }
            }
        }

        public static void RegisterWardBridge()
        {
            // O RaidSystem, se estiver instalado, registra o Guilds dele depois e prevalece.
            Wards.WardBridge.GuildOfPlayer = playerId =>
                PvpConfig.Active && PvpConfig.ClanEnabled.Value ? ClanOf(playerId) : null;
            Wards.WardBridge.LiveGuildLookup = true;
        }

        // ------------------------------------------------------------------- servidor

        private sealed class Invite
        {
            public string Clan;
            public string From;
            public double Until;
        }

        private static readonly Dictionary<long, Invite> _invites = new Dictionary<long, Invite>();
        private static float _nextPositions;

        private static PvpClanRecord FindClan(string name)
            => PvpStore.Data.clans.FirstOrDefault(c => string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase));

        private static PvpClanRecord ClanOfServer(long playerId)
            => PvpStore.Data.clans.FirstOrDefault(c => c.members.Contains(playerId));

        public static void OnCommand(PvpPeer peer, string action, string arg)
        {
            if (!PvpConfig.ClanEnabled.Value)
            {
                PvpNet.Message(peer.PeerId, "O sistema de cla esta desligado neste servidor.");
                return;
            }

            action = (action ?? string.Empty).Trim().ToLowerInvariant();
            arg = (arg ?? string.Empty).Trim();
            PvpClanRecord own = ClanOfServer(peer.PlayerId);
            own?.SetName(peer.PlayerId, peer.Name);

            switch (action)
            {
                case "criar":
                case "create":
                    Create(peer, own, arg);
                    break;
                case "convidar":
                case "invite":
                    InvitePlayer(peer, own, arg);
                    break;
                case "aceitar":
                case "accept":
                    Accept(peer, own);
                    break;
                case "recusar":
                case "decline":
                    _invites.Remove(peer.PlayerId);
                    PvpNet.Message(peer.PeerId, "Convite recusado.");
                    break;
                case "sair":
                case "leave":
                    Leave(peer, own);
                    break;
                case "expulsar":
                case "kick":
                    Kick(peer, own, arg);
                    break;
                case "lider":
                case "leader":
                    Promote(peer, own, arg);
                    break;
                case "desfazer":
                case "disband":
                    Disband(peer, own);
                    break;
                default:
                    Info(peer, own);
                    break;
            }
        }

        private static void Create(PvpPeer peer, PvpClanRecord own, string name)
        {
            if (own != null)
            {
                PvpNet.Message(peer.PeerId, $"Voce ja esta no cla {own.name}. Saia antes com /cla sair.");
                return;
            }
            name = name.Replace("|", "").Replace("<", "").Replace(">", "").Trim();
            if (name.Length < MinNameLength || name.Length > MaxNameLength)
            {
                PvpNet.Message(peer.PeerId, $"Nome do cla deve ter de {MinNameLength} a {MaxNameLength} caracteres: /cla criar <nome>");
                return;
            }
            if (FindClan(name) != null)
            {
                PvpNet.Message(peer.PeerId, $"Ja existe um cla chamado {name}.");
                return;
            }

            PvpClanRecord clan = new PvpClanRecord { name = name, leader = peer.PlayerId };
            clan.members.Add(peer.PlayerId);
            clan.names.Add(peer.Name);
            PvpStore.Data.clans.Add(clan);
            PvpStore.MarkDirty();
            BroadcastDirectory();
            PvpNet.Message(peer.PeerId, $"Cla <color=#7fd4ff>{name}</color> criado. Convide com /cla convidar <jogador>.");
            Debug.Log($"[Deadheim PvP] Cla '{name}' criado por {peer.Name}.");
        }

        private static void InvitePlayer(PvpPeer peer, PvpClanRecord own, string target)
        {
            if (own == null) { PvpNet.Message(peer.PeerId, "Voce nao esta em um cla."); return; }
            if (own.leader != peer.PlayerId) { PvpNet.Message(peer.PeerId, "So o lider convida."); return; }
            if (own.members.Count >= PvpConfig.ClanMaxMembers.Value)
            {
                PvpNet.Message(peer.PeerId, $"O cla ja tem o maximo de {PvpConfig.ClanMaxMembers.Value} membros.");
                return;
            }
            if (!PvpPeer.TryFindByName(target, out PvpPeer invited))
            {
                PvpNet.Message(peer.PeerId, $"Jogador '{target}' nao esta online.");
                return;
            }
            if (ClanOfServer(invited.PlayerId) != null)
            {
                PvpNet.Message(peer.PeerId, $"{invited.Name} ja esta em um cla.");
                return;
            }

            _invites[invited.PlayerId] = new Invite { Clan = own.name, From = peer.Name, Until = PvpState.Now + InviteSeconds };
            PvpNet.Message(invited.PeerId, $"{peer.Name} convidou voce para o cla <color=#7fd4ff>{own.name}</color>. " +
                                           "Digite /cla aceitar (vale por 2 min).");
            PvpNet.Message(peer.PeerId, $"Convite enviado para {invited.Name}.", false);
        }

        private static void Accept(PvpPeer peer, PvpClanRecord own)
        {
            if (own != null) { PvpNet.Message(peer.PeerId, $"Voce ja esta no cla {own.name}."); return; }
            if (!_invites.TryGetValue(peer.PlayerId, out Invite invite) || invite.Until < PvpState.Now)
            {
                _invites.Remove(peer.PlayerId);
                PvpNet.Message(peer.PeerId, "Voce nao tem convite valido.");
                return;
            }
            _invites.Remove(peer.PlayerId);

            PvpClanRecord clan = FindClan(invite.Clan);
            if (clan == null) { PvpNet.Message(peer.PeerId, "Esse cla nao existe mais."); return; }
            if (clan.members.Count >= PvpConfig.ClanMaxMembers.Value) { PvpNet.Message(peer.PeerId, "O cla esta cheio."); return; }

            clan.members.Add(peer.PlayerId);
            while (clan.names.Count < clan.members.Count - 1) clan.names.Add(string.Empty);
            clan.names.Add(peer.Name);
            PvpStore.MarkDirty();
            BroadcastDirectory();
            TellClan(clan, $"<color=#7fd4ff>{peer.Name}</color> entrou no cla {clan.name}.");
        }

        private static void Leave(PvpPeer peer, PvpClanRecord own)
        {
            if (own == null) { PvpNet.Message(peer.PeerId, "Voce nao esta em um cla."); return; }
            RemoveMember(own, peer.PlayerId);
            PvpNet.Message(peer.PeerId, $"Voce saiu do cla {own.name}.");
            TellClan(own, $"{peer.Name} saiu do cla.");
        }

        private static void Kick(PvpPeer peer, PvpClanRecord own, string target)
        {
            if (own == null || own.leader != peer.PlayerId) { PvpNet.Message(peer.PeerId, "So o lider expulsa."); return; }
            int index = own.names.FindIndex(n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= own.members.Count) { PvpNet.Message(peer.PeerId, $"'{target}' nao e membro do cla."); return; }
            long kicked = own.members[index];
            if (kicked == peer.PlayerId) { PvpNet.Message(peer.PeerId, "Para sair use /cla sair."); return; }

            string name = own.NameOf(kicked);
            RemoveMember(own, kicked);
            TellClan(own, $"{name} foi expulso do cla.");
            if (PvpPeer.TryFindByPlayerId(kicked, out PvpPeer kickedPeer))
                PvpNet.Message(kickedPeer.PeerId, $"Voce foi expulso do cla {own.name}.");
        }

        private static void Promote(PvpPeer peer, PvpClanRecord own, string target)
        {
            if (own == null || own.leader != peer.PlayerId) { PvpNet.Message(peer.PeerId, "So o lider passa a lideranca."); return; }
            int index = own.names.FindIndex(n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= own.members.Count) { PvpNet.Message(peer.PeerId, $"'{target}' nao e membro do cla."); return; }
            own.leader = own.members[index];
            PvpStore.MarkDirty();
            TellClan(own, $"{own.NameOf(own.leader)} agora lidera o cla.");
        }

        private static void Disband(PvpPeer peer, PvpClanRecord own)
        {
            if (own == null || own.leader != peer.PlayerId) { PvpNet.Message(peer.PeerId, "So o lider desfaz o cla."); return; }
            TellClan(own, $"O cla {own.name} foi desfeito.");
            PvpStore.Data.clans.Remove(own);
            PvpStore.MarkDirty();
            BroadcastDirectory();
        }

        private static void Info(PvpPeer peer, PvpClanRecord own)
        {
            StringBuilder text = new StringBuilder();
            if (own == null)
            {
                text.Append("Voce nao esta em um cla.");
            }
            else
            {
                HashSet<long> online = new HashSet<long>(PvpPeer.Online().Select(p => p.PlayerId));
                text.Append($"Cla <color=#7fd4ff>{own.name}</color> ({own.members.Count}/{PvpConfig.ClanMaxMembers.Value}):");
                foreach (long member in own.members)
                {
                    text.Append("\n  ").Append(own.NameOf(member));
                    if (member == own.leader) text.Append(" (lider)");
                    if (online.Contains(member)) text.Append(" <color=#7CFC00>online</color>");
                }
            }
            text.Append("\nComandos: /cla criar <nome> | convidar <jogador> | aceitar | recusar | sair | expulsar <jogador> | lider <jogador> | desfazer");
            PvpNet.Message(peer.PeerId, text.ToString(), false);
        }

        private static void RemoveMember(PvpClanRecord clan, long playerId)
        {
            clan.Remove(playerId);
            if (clan.members.Count == 0) PvpStore.Data.clans.Remove(clan);
            else if (clan.leader == playerId) clan.leader = clan.members[0];
            PvpStore.MarkDirty();
            BroadcastDirectory();
        }

        private static void TellClan(PvpClanRecord clan, string text)
        {
            foreach (long member in clan.members)
                if (PvpPeer.TryFindByPlayerId(member, out PvpPeer peer))
                    PvpNet.Message(peer.PeerId, text, false);
        }

        private static ZPackage DirectoryPackage()
        {
            List<KeyValuePair<long, string>> entries = new List<KeyValuePair<long, string>>();
            foreach (PvpClanRecord clan in PvpStore.Data.clans)
                foreach (long member in clan.members)
                    entries.Add(new KeyValuePair<long, string>(member, clan.name));

            ZPackage pkg = PvpNet.Package(PvpNet.OpClanDirectory);
            pkg.Write(entries.Count);
            foreach (KeyValuePair<long, string> entry in entries)
            {
                pkg.Write(entry.Key);
                pkg.Write(entry.Value);
            }
            return pkg;
        }

        public static void SendDirectory(long peerId) => PvpNet.SendToClient(peerId, DirectoryPackage());

        public static void BroadcastDirectory() => PvpNet.SendToEveryone(DirectoryPackage());

        /// <summary>A cada 2 s cada membro online recebe a posicao dos colegas online.</summary>
        public static void ServerTick()
        {
            if (!PvpConfig.ClanEnabled.Value || !PvpConfig.ClanShowOnMap.Value) return;
            if (Time.time < _nextPositions) return;
            _nextPositions = Time.time + 2f;

            List<PvpPeer> online = PvpPeer.Online();
            if (online.Count < 2) return;

            foreach (PvpClanRecord clan in PvpStore.Data.clans)
            {
                List<PvpPeer> members = online.Where(p => clan.members.Contains(p.PlayerId)).ToList();
                if (members.Count < 2) continue;

                foreach (PvpPeer receiver in members)
                {
                    ZPackage pkg = PvpNet.Package(PvpNet.OpClanPositions);
                    pkg.Write(members.Count - 1);
                    foreach (PvpPeer mate in members)
                    {
                        if (mate.PlayerId == receiver.PlayerId) continue;
                        pkg.Write(mate.Name);
                        pkg.Write(mate.CharacterId);
                        pkg.Write(mate.Position);
                    }
                    PvpNet.SendToClient(receiver.PeerId, pkg);
                }
            }
        }
    }
}

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Fluxo de entrada exclusivo do Deadheim. O launcher apenas entrega o
    /// +connect; este mod escolhe/cria e vincula um único personagem ao servidor.
    /// </summary>
    internal static class DirectJoinFlow
    {
        private const string CharacterFileName = "Detalhes.Deadheim.directjoin.character";
        private static readonly MethodInfo SetSelectedProfile = typeof(FejdStartup)
            .GetMethod("SetSelectedProfile", BindingFlags.Instance | BindingFlags.NonPublic);

        private static ManualLogSource _log;
        private static string _server;
        private static bool _menuHandled;
        private static bool _awaitingNewCharacter;

        internal static void Initialize(ManualLogSource log)
        {
            _log = log;
            _server = ReadConnectTarget(Environment.GetCommandLineArgs());
            if (string.IsNullOrWhiteSpace(_server)) return;

            var host = new GameObject("Deadheim.DirectJoinFlow");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<DirectJoinBehaviour>();
            _log.LogInfo("Conexão direta do Deadheim ativa para " + _server);
        }

        private static string ReadConnectTarget(string[] args)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "+connect", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1].Trim().ToLowerInvariant();
            return null;
        }

        private static void TryHandleCharacterMenu()
        {
            if (_menuHandled || string.IsNullOrWhiteSpace(_server)) return;
            var startup = FejdStartup.instance;
            if (startup == null || startup.m_characterSelectScreen == null ||
                !startup.m_characterSelectScreen.activeInHierarchy) return;

            var filename = ReadMappedCharacter();

            if (!string.IsNullOrWhiteSpace(filename) && SelectExisting(startup, filename))
            {
                _menuHandled = true;
                _log.LogInfo("Personagem vinculado selecionado; entrando no servidor.");
                startup.OnCharacterStart();
                return;
            }

            _menuHandled = true;
            _awaitingNewCharacter = true;
            _log.LogInfo("Sem vínculo para este servidor; abrindo criação de personagem (Cancelar volta para a seleção).");
            startup.OnCharacterNew();
        }

        /// <summary>
        /// Cancelar a criação devolve o jogador para a lista de personagens, e a
        /// partir daí o fluxo é o normal do jogo: escolher um e clicar em Iniciar
        /// entra no servidor, porque o +connect deixou a entrada na fila e ela
        /// continua lá.
        ///
        /// Existe porque a criação não é sempre o que a pessoa precisa. O vínculo
        /// é um arquivo local, então some por motivos que nada têm a ver com ser
        /// novato: computador novo, perfil reinstalado, ou o endereço do +connect
        /// mudando (o vínculo guarda host:porta e compara texto exato, então uma
        /// troca de porta faz TODO mundo virar "primeiro acesso" de uma vez).
        /// Quem já jogava caía na criação, digitava um nome novo, e o
        /// ServerCharacters recusava: "You are not allowed to create more than one
        /// character on this server." Ficava sem conseguir entrar, com o
        /// personagem antigo intacto no servidor e invisível para ele.
        ///
        /// Abrir a criação segue sendo o certo por padrão -- o primeiro acesso
        /// deve gerar um personagem dedicado ao servidor, e não trazer um de
        /// partida solo, que é o que o ServerCharacters avisa para não fazer. O
        /// que faltava era a saída para quem não é novato.
        ///
        /// Sem zerar a espera aqui, o Postfix de OnNewCharacterDone continuaria
        /// armado depois do cancelamento e dispararia um OnCharacterStart que o
        /// jogador não pediu.
        /// </summary>
        internal static void OnNewCharacterCancelled()
        {
            if (!_awaitingNewCharacter) return;
            _awaitingNewCharacter = false;
            _log?.LogInfo("Criação cancelada; a seleção de personagem fica com o jogador.");
        }

        private static bool SelectExisting(FejdStartup startup, string filename)
        {
            var exists = SaveSystem.GetAllPlayerProfiles()
                .Any(profile => string.Equals(profile.GetFilename(), filename, StringComparison.OrdinalIgnoreCase));
            if (!exists || SetSelectedProfile == null) return false;
            SetSelectedProfile.Invoke(startup, new object[] { filename });
            return true;
        }

        private static string ReadMappedCharacter()
        {
            try
            {
                if (!File.Exists(CharacterPath)) return null;
                var lines = File.ReadAllLines(CharacterPath);
                if (lines.Length != 2 || Decode(lines[0]) != _server) return null;
                return Decode(lines[1]);
            }
            catch (Exception ex)
            {
                _log?.LogWarning("Não foi possível ler o vínculo de personagem: " + ex.Message);
                return null;
            }
        }

        internal static void OnNewCharacterDone(FejdStartup startup)
        {
            if (!_awaitingNewCharacter || startup.m_newCharacterPanel.activeInHierarchy) return;
            _awaitingNewCharacter = false;
            startup.OnCharacterStart();
        }

        internal static void RecordSpawnedCharacter()
        {
            if (string.IsNullOrWhiteSpace(_server) || Game.instance == null) return;
            var profile = Game.instance.GetPlayerProfile();
            if (profile == null || string.IsNullOrWhiteSpace(profile.GetFilename())) return;

            try
            {
                File.WriteAllLines(CharacterPath, new[] { Encode(_server), Encode(profile.GetFilename()) });
                _log?.LogInfo("Personagem vinculado a este servidor pelo Deadheim.");
            }
            catch (Exception ex)
            {
                _log?.LogWarning("Não foi possível salvar o vínculo de personagem: " + ex.Message);
            }
        }

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string CharacterPath => Path.Combine(Paths.ConfigPath, CharacterFileName);

        private sealed class DirectJoinBehaviour : MonoBehaviour
        {
            private void Update() => TryHandleCharacterMenu();
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnNewCharacterDone))]
        private static class NewCharacterDonePatch
        {
            private static void Postfix(FejdStartup __instance) => OnNewCharacterDone(__instance);
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnNewCharacterCancel))]
        private static class NewCharacterCancelPatch
        {
            private static void Postfix() => OnNewCharacterCancelled();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class PlayerSpawnedPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance == Player.m_localPlayer) RecordSpawnedCharacter();
            }
        }
    }
}

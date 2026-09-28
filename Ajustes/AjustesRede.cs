using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Deadheim.Ajustes
{
    /// <summary>
    /// A aba "Servidor" da janela: o cliente de um admin pede ao servidor a lista de mods, as
    /// opcoes de um mod e a troca de um valor. Quem aplica e o proprio servidor, no ConfigFile
    /// dele, entao vale para qualquer mod que o servidor tenha (inclusive opcoes que so existem
    /// la). Um canal em cada direcao, com a operacao como primeira string, como no PvpNet.
    /// </summary>
    internal static class AjustesRede
    {
        public const string ParaServidor = "DH_Ajustes_ToServer";
        public const string ParaCliente = "DH_Ajustes_ToClient";

        private const string OpMods = "mods";
        private const string OpItens = "itens";
        private const string OpDefinir = "definir";
        private const string OpNegado = "negado";

        private static bool _registrado;

        /// <summary>
        /// A conexao cujo pacote esta sendo tratado agora. O "sender" de um RPC roteado vem
        /// escrito pelo proprio cliente e o servidor nao confere, entao a checagem de admin
        /// usa a conexao de verdade (o mesmo truque do ServerSync). Null = chamada local.
        /// </summary>
        private static ZRpc _conexaoAtual;

        [HarmonyPatch(typeof(ZRpc), "HandlePackage")]
        private static class ConexaoAtualPatch
        {
            private static void Prefix(ZRpc __instance, out ZRpc __state)
            {
                __state = _conexaoAtual;
                _conexaoAtual = __instance;
            }

            private static Exception Finalizer(Exception __exception, ZRpc __state)
            {
                _conexaoAtual = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegistroPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(ParaServidor, NoServidor);
                ZRoutedRpc.instance.Register<ZPackage>(ParaCliente, NoCliente);
                _registrado = true;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Postfix() => _registrado = false;
        }

        // ------------------------------------------------------------------ cliente

        public static bool PedirMods() => Enviar(Pacote(OpMods));

        public static bool PedirItens(string guid)
        {
            ZPackage pkg = Pacote(OpItens);
            pkg.Write(guid);
            return Enviar(pkg);
        }

        public static bool Definir(string guid, string secao, string chave, string valor)
        {
            ZPackage pkg = Pacote(OpDefinir);
            pkg.Write(guid);
            pkg.Write(secao);
            pkg.Write(chave);
            pkg.Write(valor ?? "");
            return Enviar(pkg);
        }

        private static bool Enviar(ZPackage pkg)
        {
            if (!_registrado || ZRoutedRpc.instance == null) return false;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ParaServidor, pkg);
            return true;
        }

        private static void NoCliente(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;

            try
            {
                string op = pkg.ReadString();
                switch (op)
                {
                    case OpMods:
                    {
                        int total = pkg.ReadInt();
                        List<ModAjustes> mods = new List<ModAjustes>(total);
                        for (int i = 0; i < total; i++)
                        {
                            mods.Add(new ModAjustes
                            {
                                Guid = pkg.ReadString(),
                                Nome = pkg.ReadString(),
                                Versao = pkg.ReadString(),
                                Itens = pkg.ReadInt()
                            });
                        }
                        AjustesJanela.ReceberModsDoServidor(mods);
                        break;
                    }
                    case OpItens:
                    {
                        string guid = pkg.ReadString();
                        ZPackage dados = new ZPackage(Descomprimir(pkg.ReadByteArray()));
                        int total = dados.ReadInt();
                        List<ItemAjuste> itens = new List<ItemAjuste>(total);
                        for (int i = 0; i < total; i++) itens.Add(ItemAjuste.Ler(dados));
                        AjustesJanela.ReceberItensDoServidor(guid, itens);
                        break;
                    }
                    case OpDefinir:
                    {
                        bool ok = pkg.ReadBool();
                        string guid = pkg.ReadString();
                        string secao = pkg.ReadString();
                        string chave = pkg.ReadString();
                        string valor = pkg.ReadString();
                        string mensagem = pkg.ReadString();
                        AjustesJanela.ReceberResultadoDoServidor(ok, guid, secao, chave, valor, mensagem);
                        break;
                    }
                    case OpNegado:
                        AjustesJanela.ReceberNegado(pkg.ReadString());
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Ajustes: resposta do servidor ilegivel: {ex}");
            }
        }

        // ----------------------------------------------------------------- servidor

        private static void NoServidor(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            string op;
            try { op = pkg.ReadString(); }
            catch (Exception) { return; }

            if (!EhAdmin(out string quem))
            {
                Debug.LogWarning($"[Deadheim] Ajustes: {quem} pediu '{op}' sem ser admin.");
                ZPackage negado = Pacote(OpNegado);
                negado.Write("So admins do servidor (adminlist.txt) podem mexer na config do servidor.");
                Responder(sender, negado);
                return;
            }

            try
            {
                switch (op)
                {
                    case OpMods:
                        ResponderMods(sender);
                        break;
                    case OpItens:
                        ResponderItens(sender, pkg.ReadString());
                        break;
                    case OpDefinir:
                        ResponderDefinir(sender, quem, pkg.ReadString(), pkg.ReadString(), pkg.ReadString(), pkg.ReadString());
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Ajustes: falha tratando '{op}' de {quem}: {ex}");
            }
        }

        private static void ResponderMods(long sender)
        {
            List<ModAjustes> mods = AjustesDados.Mods(local: false);
            ZPackage pkg = Pacote(OpMods);
            pkg.Write(mods.Count);
            foreach (ModAjustes mod in mods)
            {
                pkg.Write(mod.Guid);
                pkg.Write(mod.Nome);
                pkg.Write(mod.Versao);
                pkg.Write(mod.Itens);
            }
            Responder(sender, pkg);
        }

        private static void ResponderItens(long sender, string guid)
        {
            List<ItemAjuste> itens = AjustesDados.Itens(guid, local: false);
            ZPackage dados = new ZPackage();
            dados.Write(itens.Count);
            foreach (ItemAjuste item in itens) item.Escrever(dados);

            // Mod grande (CLLC, Jewelcrafting) passa de centenas de opcoes com descricao:
            // comprimido, cabe folgado num pacote so.
            ZPackage pkg = Pacote(OpItens);
            pkg.Write(guid);
            pkg.Write(Comprimir(dados.GetArray()));
            Responder(sender, pkg);
        }

        private static void ResponderDefinir(long sender, string quem, string guid, string secao, string chave, string valor)
        {
            bool ok = AjustesDados.Aplicar(guid, secao, chave, valor, local: false,
                out string anterior, out string atual, out string erro);

            if (ok)
                Debug.Log($"[Deadheim] Ajustes: {quem} mudou {guid} [{secao}] {chave}: '{anterior}' -> '{atual}'");
            else
                Debug.LogWarning($"[Deadheim] Ajustes: {quem} tentou mudar {guid} [{secao}] {chave} para '{valor}': {erro}");

            ZPackage pkg = Pacote(OpDefinir);
            pkg.Write(ok);
            pkg.Write(guid);
            pkg.Write(secao);
            pkg.Write(chave);
            pkg.Write(atual);
            pkg.Write(ok ? "" : erro);
            Responder(sender, pkg);
        }

        /// <summary>
        /// Admin pela adminlist.txt do servidor, a mesma lista que o ServerSync usa para
        /// liberar config travada. Sem conexao (chamada local) so pode ser o host de um
        /// mundo nao dedicado falando com o proprio servidor.
        /// </summary>
        private static bool EhAdmin(out string quem)
        {
            ZRpc conexao = _conexaoAtual;
            if (conexao == null)
            {
                quem = "host";
                return !ZNet.instance.IsDedicated();
            }

            string host = conexao.GetSocket()?.GetHostName();
            quem = string.IsNullOrEmpty(host) ? "?" : host;
            return !string.IsNullOrEmpty(host) && ZNet.instance.IsAdmin(host);
        }

        private static void Responder(long destino, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(destino, ParaCliente, pkg);
        }

        private static ZPackage Pacote(string op)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(op);
            return pkg;
        }

        private static byte[] Comprimir(byte[] dados)
        {
            using (MemoryStream saida = new MemoryStream())
            {
                using (DeflateStream deflate = new DeflateStream(saida, CompressionLevel.Optimal))
                    deflate.Write(dados, 0, dados.Length);
                return saida.ToArray();
            }
        }

        private static byte[] Descomprimir(byte[] dados)
        {
            using (MemoryStream entrada = new MemoryStream(dados))
            using (DeflateStream deflate = new DeflateStream(entrada, CompressionMode.Decompress))
            using (MemoryStream saida = new MemoryStream())
            {
                deflate.CopyTo(saida);
                return saida.ToArray();
            }
        }
    }
}

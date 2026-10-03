// Stubs FUNCIONAIS para o teste sem o jogo: a rede do Valheim simulada num processo so.
// Mesmos nomes e assinaturas que o AjustesRede usa do assembly_valheim.
using System;
using System.Collections.Generic;
using System.IO;

public class ZPackage
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;
    private readonly BinaryReader _reader;
    public ZPackage() { _stream = new MemoryStream(); _writer = new BinaryWriter(_stream); _reader = new BinaryReader(_stream); }
    public ZPackage(byte[] data) : this() { _writer.Write(data); _stream.Position = 0; }
    public void Write(string v) => _writer.Write(v);   // BinaryWriter lanca com null, como o ZPackage do jogo
    public void Write(int v) => _writer.Write(v);
    public void Write(long v) => _writer.Write(v);
    public void Write(bool v) => _writer.Write(v);
    public void Write(byte[] v) { _writer.Write(v.Length); _writer.Write(v); }
    public string ReadString() => _reader.ReadString();
    public int ReadInt() => _reader.ReadInt32();
    public long ReadLong() => _reader.ReadInt64();
    public bool ReadBool() => _reader.ReadBoolean();
    public byte[] ReadByteArray() => _reader.ReadBytes(_reader.ReadInt32());
    public byte[] GetArray() => _stream.ToArray();
    public int GetPos() => (int)_stream.Position;
    public void SetPos(int pos) => _stream.Position = pos;
}

public interface ISocket { string GetHostName(); }
public class FakeSocket : ISocket { public string Host; public string GetHostName() => Host; }

public class ZRpc
{
    public ISocket Socket;
    public Action<ZPackage> OnPackage;
    public ISocket GetSocket() => Socket;
    // Mesmo nome e visibilidade do jogo: e aqui que o AjustesRede se pendura.
    private void HandlePackage(ZPackage package) => OnPackage(package);
}

public class ZNetPeer
{
    public ZRpc m_rpc;
    public long m_uid;
    public ISocket m_socket;
}

public class ZNet
{
    public static ZNet instance;
    public bool Server = true, Dedicated = true;
    public HashSet<string> Admins = new HashSet<string>();
    public readonly List<ZNetPeer> Peers = new List<ZNetPeer>();
    public bool IsServer() => Server;
    public bool IsDedicated() => Dedicated;
    public bool IsAdmin(string host) => Admins.Contains(host);
    public List<ZNetPeer> GetPeers() => Peers;
}

public class ZRoutedRpc
{
    public static ZRoutedRpc instance;
    public static long Everybody = 0;
    public long ServerId = 1;
    public long m_id = 1;
    /// <summary>O remetente que o handler do jogo leu do ultimo pacote (RPC_RoutedRPC).</summary>
    public long UltimoRemetente;
    // Mesmo nome e visibilidade do jogo: o RemetenteRpc reescreve o remetente antes desta leitura.
    private void RPC_RoutedRPC(ZRpc rpc, ZPackage pkg)
    {
        pkg.ReadLong();
        UltimoRemetente = pkg.ReadLong();
    }
    public readonly Dictionary<string, Delegate> Handlers = new Dictionary<string, Delegate>();
    public readonly List<string> Sent = new List<string>();
    public Action<long, string, ZPackage> Router;
    public long GetServerPeerID() => ServerId;
    public void Register<T>(string name, Action<long, T> f) => Handlers[name] = f;
    public void InvokeRoutedRPC(long target, string name, params object[] args)
    {
        Sent.Add(name);
        // Como no jogo: o destino le um pacote novo, do comeco.
        Router(target, name, new ZPackage(((ZPackage)args[0]).GetArray()));
    }
    public void Deliver(string name, long sender, ZPackage pkg) => ((Action<long, ZPackage>)Handlers[name])(sender, pkg);
}

public class Game
{
    private void Start() { }
    public void Logout(bool save = true, bool changeToStartScene = true) { }
}

namespace Deadheim { public class Plugin { public const string PluginGUID = "Detalhes.Deadheim"; } }

namespace Deadheim.Ajustes
{
    // Sombreia o UnityEngine.Debug dentro do namespace (o de verdade precisa da Unity).
    internal static class Debug
    {
        public static readonly List<string> Linhas = new List<string>();
        public static void Log(object m) { Linhas.Add("INFO " + m); }
        public static void LogWarning(object m) { Linhas.Add("WARN " + m); }
        public static void LogError(object m) { Linhas.Add("ERRO " + m); }
    }

    // A janela de verdade e Unity; aqui ela so anota o que o AjustesRede entregou.
    internal static class AjustesJanela
    {
        public static List<ModAjustes> Mods;
        public static string ItensGuid;
        public static List<ItemAjuste> Itens;
        public static string Negado;
        public static object[] Resultado;
        public static void Limpar() { Mods = null; ItensGuid = null; Itens = null; Negado = null; Resultado = null; }
        public static void ReceberModsDoServidor(List<ModAjustes> m) => Mods = m;
        public static void ReceberItensDoServidor(string g, List<ItemAjuste> i) { ItensGuid = g; Itens = i; }
        public static void ReceberResultadoDoServidor(bool ok, string g, string s, string k, string v, string msg) => Resultado = new object[] { ok, g, s, k, v, msg };
        public static void ReceberNegado(string msg) => Negado = msg;
    }
}

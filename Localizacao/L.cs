using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;

namespace Deadheim
{
    /// <summary>
    /// Localizacao PT/EN do Deadheim. Textos ficam em Localizacao/Textos.*.cs (um arquivo por modulo),
    /// registrados com L.Add(chave, pt, en). No codigo: L.T("modulo.chave", args...) (string.Format).
    /// Para campos que o jogo traduz sozinho (m_name, m_description): L.Tok("chave") devolve "$dh_chave".
    /// Idioma: config General/Idioma (auto|pt|en). "auto" segue o idioma do jogo (Portuguese* = pt, resto = en);
    /// servidor dedicado (sem Localization) usa pt.
    /// </summary>
    public static class L
    {
        public const string Prefixo = "$dh_";
        private static readonly Dictionary<string, string[]> Tabela = new Dictionary<string, string[]>();
        private static ConfigEntry<string> _cfg;

        public static void Add(string chave, string pt, string en) { Tabela[chave] = new[] { pt, en }; }

        public static void Init(ConfigFile cfg)
        {
            _cfg = cfg.Bind("General", "Idioma", "auto",
                new ConfigDescription("Idioma dos textos do Deadheim / Deadheim text language: auto, pt, en",
                    new AcceptableValueList<string>("auto", "pt", "en")));
            _cfg.SettingChanged += (a, b) => RegistrarNoJogo();
        }

        public static bool Portugues
        {
            get
            {
                string v = _cfg != null ? _cfg.Value : "auto";
                if (v == "pt") return true;
                if (v == "en") return false;
                var loc = Localization.instance;
                if (loc == null) return true;
                string lang = loc.GetSelectedLanguage() ?? "";
                return lang.StartsWith("Portuguese", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string T(string chave, params object[] args)
        {
            string txt;
            if (Tabela.TryGetValue(chave, out var par)) txt = par[Portugues ? 0 : 1];
            else txt = chave; // chave ausente fica visivel para ser corrigida
            if (args == null || args.Length == 0) return txt;
            try { return string.Format(CultureInfo.InvariantCulture, txt, args); }
            catch (FormatException) { return txt; }
        }


        // ---- Mensagens servidor -> cliente: o servidor manda a CHAVE e o cliente traduz no idioma dele.
        // Formato: chavearg1arg2 (aninhavel; pode aparecer varias vezes num texto).
        public static string Net(string chave, params object[] args)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('').Append(chave);
            if (args != null)
                foreach (object a in args) sb.Append('').Append(Convert.ToString(a, CultureInfo.InvariantCulture));
            return sb.Append('').ToString();
        }

        /// <summary>Expande os blocos Net de um texto. pt: null = idioma do jogador; true/false forca (logs do servidor = true).</summary>
        public static string Resolve(string texto, bool? pt = null)
        {
            if (string.IsNullOrEmpty(texto) || texto.IndexOf('') < 0) return texto;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < texto.Length)
            {
                int ini = texto.IndexOf('', i);
                if (ini < 0) { sb.Append(texto, i, texto.Length - i); break; }
                int prof = 0, fim = -1;
                for (int k = ini; k < texto.Length; k++)
                {
                    if (texto[k] == '') prof++;
                    else if (texto[k] == '' && --prof == 0) { fim = k; break; }
                }
                if (fim < 0) { sb.Append(texto, i, texto.Length - i); break; }
                sb.Append(texto, i, ini - i);
                var partes = new System.Collections.Generic.List<string>();
                int ps = ini + 1;
                prof = 0;
                for (int k = ini + 1; k < fim; k++)
                {
                    char c = texto[k];
                    if (c == '') prof++;
                    else if (c == '') prof--;
                    else if (c == '' && prof == 0) { partes.Add(texto.Substring(ps, k - ps)); ps = k + 1; }
                }
                partes.Add(texto.Substring(ps, fim - ps));
                object[] args = new object[partes.Count - 1];
                for (int k = 1; k < partes.Count; k++) args[k - 1] = Resolve(partes[k], pt);
                sb.Append(TIdioma(partes[0], pt ?? Portugues, args));
                i = fim + 1;
            }
            return sb.ToString();
        }

        private static string TIdioma(string chave, bool pt, object[] args)
        {
            string txt = Tabela.TryGetValue(chave, out var par) ? par[pt ? 0 : 1] : chave;
            if (args == null || args.Length == 0) return txt;
            try { return string.Format(CultureInfo.InvariantCulture, txt, args); } catch (FormatException) { return txt; }
        }

        /// <summary>Token do jogo ("$dh_chave") para m_name/m_description; resolvido por Localization.Localize.</summary>
        public static string Tok(string chave) => Prefixo + chave;

        /// <summary>Registra todas as chaves como palavras do jogo ($dh_chave) no idioma atual.</summary>
        public static void RegistrarNoJogo()
        {
            var loc = Localization.instance;
            if (loc == null) return;
            int i = Portugues ? 0 : 1;
            foreach (var kv in Tabela) loc.AddWord("dh_" + kv.Key, kv.Value[i]);
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        private static class SetupLanguagePatch
        {
            private static void Postfix() => RegistrarNoJogo();
        }
    }
}

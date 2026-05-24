using System;
using System.Net;
using System.Text;
using UnityEngine;

namespace RaidSystem
{
    public class dWebHook
    {
        public string WebHookUrl { get; set; }

        public static void SendRaidMessage(string message)
        {
            string url = RaidSystemPlugin.WebhookUrl?.Value;
            if (string.IsNullOrWhiteSpace(url)) url = RaidSystemPlugin.DefaultWebhookUrl;
            new dWebHook { WebHookUrl = url }.SendMessage(message);
        }

        public void SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(WebHookUrl) || string.IsNullOrWhiteSpace(message)) return;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(WebHookUrl);
                    req.ContentType = "application/json"; req.Method = "POST"; req.Timeout = 10000;
                    string escaped = message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                    byte[] bytes = Encoding.UTF8.GetBytes($"{{\"content\":\"{escaped}\"}}");
                    req.ContentLength = bytes.Length;
                    using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                    using var resp = (HttpWebResponse)req.GetResponse();
                }
                catch (Exception ex) { Debug.LogWarning($"[RaidSystem] Webhook: {ex.Message}"); }
            });
        }
    }
}

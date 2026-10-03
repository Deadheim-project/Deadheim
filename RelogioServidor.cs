using System;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// A hora UTC do servidor, vista do cliente sem depender do relogio do Windows.
    ///
    /// Regra que depende de horario (janela de raid do castelo, castelo que caiu) tambem roda no
    /// cliente, e o DateTime.UtcNow de la e o que o jogador quiser: adiantar a hora do PC abria a
    /// janela de raid so para ele. O servidor manda a hora dele no estado do PvP (OpState); daqui
    /// em diante o cliente soma o tempo monotonico (realtimeSinceStartup), que mudar a hora do
    /// Windows nao mexe. No servidor, e antes da primeira sincronizacao, vale o relogio local.
    /// Publico: o RaidSystem usa.
    /// </summary>
    public static class RelogioServidor
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static bool _sincronizado;
        private static double _unixDoServidor;
        private static double _monotonicoNaSincronia;

        /// <summary>O cliente ja recebeu a hora do servidor nesta sessao.</summary>
        public static bool Sincronizado => _sincronizado;

        public static DateTime UtcNow
        {
            get
            {
                if (!_sincronizado || (ZNet.instance != null && ZNet.instance.IsServer())) return DateTime.UtcNow;
                return Epoch.AddSeconds(_unixDoServidor + (Time.realtimeSinceStartupAsDouble - _monotonicoNaSincronia));
            }
        }

        /// <summary>Hora do servidor em segundos UTC desde 1970, como o servidor mandou.</summary>
        internal static void Sincronizar(double unixDoServidor)
        {
            if (unixDoServidor <= 0d) return;
            _unixDoServidor = unixDoServidor;
            _monotonicoNaSincronia = Time.realtimeSinceStartupAsDouble;
            _sincronizado = true;
        }

        /// <summary>Logout: outro servidor, outra hora.</summary>
        internal static void Esquecer() => _sincronizado = false;
    }
}

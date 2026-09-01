using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Splatform;

namespace VipList
{
    /// <summary>
    /// Shared VIP service for every mod. Consumer mods only need to reference VipList.dll
    /// and call IsVip with the player's platform user ID.
    /// </summary>
    public static class VipListApi
    {
        private static readonly char[] Separators = { ' ', '\t', '\r', '\n', ',', ';', '|' };
        private static readonly HashSet<string> VipIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static ConfigEntry<string> _config;

        /// <summary>Raised after the synchronized VIP list changes.</summary>
        public static event Action Changed;

        public static int Count => VipIds.Count;

        public static string RawValue => _config?.Value ?? string.Empty;

        public static IReadOnlyCollection<string> GetVipIds()
            => new ReadOnlyCollection<string>(new List<string>(VipIds));

        public static bool IsVip(string platformUserId)
        {
            if (string.IsNullOrWhiteSpace(platformUserId)) return false;
            return VipIds.Contains(Normalize(platformUserId));
        }

        public static bool IsVip(long platformUserId)
            => platformUserId > 0L && IsVip(platformUserId.ToString());

        public static bool IsVip(ulong platformUserId)
            => platformUserId > 0UL && IsVip(platformUserId.ToString());

        /// <summary>Checks the current Steam, Xbox, or other platform user.</summary>
        public static bool IsLocalPlayerVip()
        {
            try
            {
                IUser localUser = PlatformManager.DistributionPlatform?.LocalUser;
                return localUser != null && IsVip(localUser.PlatformUserID.m_userID);
            }
            catch
            {
                return false;
            }
        }

        internal static void Initialize(ConfigEntry<string> config)
        {
            if (_config != null)
                _config.SettingChanged -= OnSettingChanged;

            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.SettingChanged += OnSettingChanged;
            Rebuild();
        }

        private static void OnSettingChanged(object sender, EventArgs args) => Rebuild();

        private static void Rebuild()
        {
            VipIds.Clear();

            string value = _config?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (string id in value.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                {
                    string normalized = Normalize(id);
                    if (normalized.Length > 0)
                        VipIds.Add(normalized);
                }
            }

            Changed?.Invoke();
        }

        private static string Normalize(string platformUserId)
        {
            string value = (platformUserId ?? string.Empty).Trim();
            int separator = value.IndexOf('_');
            if (separator > 0 && separator < value.Length - 1)
            {
                string platform = value.Substring(0, separator);
                if (platform.Equals("Steam", StringComparison.OrdinalIgnoreCase) ||
                    platform.Equals("Xbox", StringComparison.OrdinalIgnoreCase) ||
                    platform.Equals("PlayFab", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring(separator + 1);
            }
            return value;
        }
    }
}

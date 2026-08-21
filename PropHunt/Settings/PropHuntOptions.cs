using System;
using System.Collections.Generic;
using System.IO;
using AmongUs.GameOptions;
using HarmonyLib;
using PropHunt;
using Reactor.Localization.Utilities;
using UnityEngine;

namespace PropHunt.Settings
{
    public static class PropHuntOptions
    {
        public const byte PropHuntId = 0;
        public const byte MissPenaltyId = 1;
        public const byte DisguiseRangeId = 2;
        public const byte DisguiseCooldownId = 3;

        private const string SaveFileName = "PropHunt-HostSettings";

        public static List<PropHuntOption> AllOption = new();

        /// <summary>Called from the plugin Load(). Builds defaults, restores the saved
        /// host settings and applies them to the plugin statics.</summary>
        public static void Initialize()
        {
            AllOption = BuildDefaults();
            LoadOptions();
            ApplyToPlugin();
        }

        private static List<PropHuntOption> BuildDefaults()
        {
            PropHuntPlugin plugin = PropHuntPlugin.Instance;
            bool isPropHunt = plugin?.IsPropHunt.Value ?? false;
            float missPenalty = plugin?.MissTimePenalty.Value ?? 10f;
            float range = plugin?.DisguiseRange.Value ?? 1.5f;
            float cooldown = plugin?.DisguiseCooldown.Value ?? 5f;

            string[] penaltyValues = new[] { "0", "5", "10", "15", "20", "25", "30", "35", "40", "45", "50", "55", "60" };
            int penaltyIndex = Array.IndexOf(penaltyValues, missPenalty.ToString("0"));
            if (penaltyIndex < 0) penaltyIndex = 2; // 10s default

            string[] rangeValues = new[] { "0.5", "1", "1.5", "2", "2.5", "3" };
            int rangeIndex = Array.IndexOf(rangeValues, range.ToString("0.#"));
            if (rangeIndex < 0) rangeIndex = 2; // 1.5 default

            string[] cooldownValues = new[] { "0", "3", "5", "10", "15", "20", "30" };
            int cooldownIndex = Array.IndexOf(cooldownValues, cooldown.ToString("0"));
            if (cooldownIndex < 0) cooldownIndex = 2; // 5s default

            return new List<PropHuntOption>
            {
                new PropHuntOption
                {
                    Id = PropHuntId,
                    Name = "Prop Hunt",
                    AllValues = new[] { "Off", "On" },
                    Value = (byte)(isPropHunt ? 1 : 0)
                },
                new PropHuntOption
                {
                    Id = MissPenaltyId,
                    Name = "Miss Penalty",
                    AllValues = penaltyValues,
                    Value = (byte)penaltyIndex,
                    Suffix = "s"
                },
                new PropHuntOption
                {
                    Id = DisguiseRangeId,
                    Name = "Disguise Range",
                    AllValues = rangeValues,
                    Value = (byte)rangeIndex
                },
                new PropHuntOption
                {
                    Id = DisguiseCooldownId,
                    Name = "Disguise Cooldown",
                    AllValues = cooldownValues,
                    Value = (byte)cooldownIndex,
                    Suffix = "s"
                }
            };
        }

        public static PropHuntOption Find(byte id) => AllOption.Find(o => o.Id == id);

        /// <summary>True when the room / menu is running in Hide & Seek mode.</summary>
        public static bool IsHideNSeek()
        {
            if (GameOptionsManager.Instance == null) return false;
            return GameOptionsManager.Instance.currentGameMode == GameModes.HideNSeek;
        }

        /// <summary>Copies the option list into the plugin statics and config.</summary>
        public static void ApplyToPlugin()
        {
            PropHuntOption propHunt = Find(PropHuntId);
            PropHuntOption penalty = Find(MissPenaltyId);
            PropHuntOption range = Find(DisguiseRangeId);
            PropHuntOption cooldown = Find(DisguiseCooldownId);

            PropHuntPlugin.isPropHunt = propHunt != null && propHunt.Value == 1;
            PropHuntPlugin.missTimePenalty = penalty != null && float.TryParse(penalty.AllValues[penalty.Value], out float v) ? v : 10f;
            PropHuntPlugin.disguiseRange = range != null && float.TryParse(range.AllValues[range.Value], out float r) ? r : 1.5f;
            PropHuntPlugin.disguiseCooldown = cooldown != null && float.TryParse(cooldown.AllValues[cooldown.Value], out float c) ? c : 5f;

            PropHuntPlugin.Instance.IsPropHunt.Value = PropHuntPlugin.isPropHunt;
            PropHuntPlugin.Instance.MissTimePenalty.Value = PropHuntPlugin.missTimePenalty;
            PropHuntPlugin.Instance.DisguiseRange.Value = PropHuntPlugin.disguiseRange;
            PropHuntPlugin.Instance.DisguiseCooldown.Value = PropHuntPlugin.disguiseCooldown;
            PropHuntPlugin.Instance.Config.Save();
        }

        /// <summary>Refreshes the option list from the plugin statics (used when a
        /// settings RPC arrives so the menu stays in sync on every client).</summary>
        public static void UpdateFromPlugin()
        {
            PropHuntOption propHunt = Find(PropHuntId);
            PropHuntOption penalty = Find(MissPenaltyId);
            PropHuntOption range = Find(DisguiseRangeId);
            PropHuntOption cooldown = Find(DisguiseCooldownId);

            if (propHunt != null) propHunt.Value = (byte)(PropHuntPlugin.isPropHunt ? 1 : 0);
            if (penalty != null)
            {
                int index = Array.IndexOf(penalty.AllValues, PropHuntPlugin.missTimePenalty.ToString("0"));
                penalty.Value = (byte)Math.Max(0, index);
            }
            if (range != null)
            {
                int index = Array.IndexOf(range.AllValues, PropHuntPlugin.disguiseRange.ToString("0.#"));
                range.Value = (byte)Math.Max(0, index);
            }
            if (cooldown != null)
            {
                int index = Array.IndexOf(cooldown.AllValues, PropHuntPlugin.disguiseCooldown.ToString("0"));
                cooldown.Value = (byte)Math.Max(0, index);
            }
        }

        /// <summary>Host: broadcasts the full current state to all clients.</summary>
        public static void SyncOptions()
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (PlayerControl.LocalPlayer == null) return;
            RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, PropHuntPlugin.isPropHunt, PropHuntPlugin.missTimePenalty, PropHuntPlugin.disguiseRange, PropHuntPlugin.disguiseCooldown);
        }

        /// <summary>Host: applies a menu change, persists it, pushes it to everyone.</summary>
        public static void RpcPushDelta(byte optionId, byte value)
        {
            PropHuntOption option = Find(optionId);
            if (option == null) return;

            option.Value = value;
            ApplyToPlugin();
            SaveOptions();
            SyncOptions();
            NotifyOptionChanged(option);
        }

        /// <summary>Shows the vanilla "X changed setting" toast for our options.</summary>
        public static void NotifyOptionChanged(PropHuntOption option)
        {
            if (DestroyableSingleton<HudManager>.Instance == null || DestroyableSingleton<HudManager>.Instance.Notifier == null) return;
            if (option.StringName == (StringNames)0) option.StringName = CustomStringName.CreateAndRegister(option.Name);
            DestroyableSingleton<HudManager>.Instance.Notifier.AddSettingsChangeMessage(option.StringName, option.AllValues[option.Value] + option.Suffix, false, RoleTypes.Crewmate);

            if (DestroyableSingleton<LobbyInfoPane>.InstanceExists && DestroyableSingleton<LobbyInfoPane>.Instance != null)
            {
                DestroyableSingleton<LobbyInfoPane>.Instance.RefreshPane();
            }
        }

        /// <summary>Notification helper used by the settings RPC handler.</summary>
        public static void ShowSettingNotification(string name, string value)
        {
            if (DestroyableSingleton<HudManager>.Instance == null || DestroyableSingleton<HudManager>.Instance.Notifier == null) return;
            StringNames stringName = CustomStringName.CreateAndRegister(name);
            DestroyableSingleton<HudManager>.Instance.Notifier.AddSettingsChangeMessage(stringName, value, false, RoleTypes.Crewmate);

            if (DestroyableSingleton<LobbyInfoPane>.InstanceExists && DestroyableSingleton<LobbyInfoPane>.Instance != null)
            {
                DestroyableSingleton<LobbyInfoPane>.Instance.RefreshPane();
            }
        }

        public static void SaveOptions()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < AllOption.Count; i++)
                {
                    sb.Append(AllOption[i].Id).Append(',').Append(AllOption[i].Value).Append(';');
                }
                File.WriteAllText(Path.Combine(Application.persistentDataPath, SaveFileName), sb.ToString());
            }
            catch { }
        }

        public static void LoadOptions()
        {
            try
            {
                List<PropHuntOption> defaults = BuildDefaults();
                string path = Path.Combine(Application.persistentDataPath, SaveFileName);
                if (File.Exists(path))
                {
                    Dictionary<byte, byte> saved = new Dictionary<byte, byte>();
                    foreach (string segment in File.ReadAllText(path).Split(';'))
                    {
                        int splitter = segment.IndexOf(',');
                        if (splitter == -1) break;
                        if (byte.TryParse(segment[..splitter], out byte id) && byte.TryParse(segment[(splitter + 1)..], out byte value))
                        {
                            saved[id] = value;
                        }
                    }
                    for (int i = 0; i < defaults.Count; i++)
                    {
                        if (saved.TryGetValue(defaults[i].Id, out byte value) && value < defaults[i].AllValues.Length)
                        {
                            defaults[i].Value = value;
                        }
                    }
                }
                AllOption = defaults;
            }
            catch
            {
                AllOption = BuildDefaults();
            }
        }

        // Re-broadcast the current settings whenever a player's spawn coroutine runs
        // (the host broadcasts to all clients, mirroring Among-Chess's CoSpawnPlayer patch).
        [HarmonyPatch(typeof(PlayerPhysics._CoSpawnPlayer_d__42), "MoveNext")]
        public static class CoSpawnPlayerPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                SyncOptions();
            }
        }
    }
}

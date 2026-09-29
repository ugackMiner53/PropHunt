// Core Script of PropHuntPlugin
// Copyright (C) 2024 ugackMiner

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PropHunt.Settings;
using Reactor;
using Reactor.Utilities;

namespace PropHunt;

[BepInPlugin("com.ugackminer.amongus.prophunt", "Prop Hunt", "v2026.9.29")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public partial class PropHuntPlugin : BasePlugin
{
    // Backend Variables
    public Harmony Harmony { get; } = new("com.ugackminer.amongus.prophunt");
    public ConfigEntry<bool> IsPropHunt { get; private set; }
    public ConfigEntry<float> MissTimePenalty { get; private set; }
    public ConfigEntry<float> DisguiseRange { get; private set; }
    public ConfigEntry<float> DisguiseCooldown { get; private set; }
    public ConfigEntry<float> SeekerWaitTime { get; private set; }
    public ConfigEntry<bool> InfectionMode { get; private set; }

    // Gameplay Variables
    public static bool isPropHunt = true;
    public static float missTimePenalty = 10f;
    public static float disguiseRange = 1.5f;
    public static float disguiseCooldown = 5f;
    public static float seekerWaitTime = 10f;
    public static bool infectionMode = false;

    // Constants
    public const float propMoveSpeed = 0.5f;
    public const float maxPropDistance = 0.6f;

    public static PropHuntPlugin Instance;

    public override void Load()
    {
        ReactorCredits.Register("Prop Hunt", "v2026.9.29", false, ReactorCredits.AlwaysShow);

        Instance = PluginSingleton<PropHuntPlugin>.Instance;

        IsPropHunt = Config.Bind("Prop Hunt", "Prop Hunt", false);
        MissTimePenalty = Config.Bind("Prop Hunt", "Miss Penalty", 10f);
        DisguiseRange = Config.Bind("Prop Hunt", "Disguise Range", 1.5f);
        DisguiseCooldown = Config.Bind("Prop Hunt", "Disguise Cooldown", 5f);
        SeekerWaitTime = Config.Bind("Prop Hunt", "Seeker Wait Time", 10f);
        InfectionMode = Config.Bind("Prop Hunt", "Infection Mode", false);

        // Restore the persisted settings into the gameplay statics
        isPropHunt = IsPropHunt.Value;
        missTimePenalty = MissTimePenalty.Value;
        disguiseRange = DisguiseRange.Value;
        disguiseCooldown = DisguiseCooldown.Value;
        seekerWaitTime = SeekerWaitTime.Value;
        infectionMode = InfectionMode.Value;

        PropHuntPreset.SetupPreset();
        PropHuntOptions.Initialize();

        Harmony.PatchAll();
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class HudUpdatePatch
    {
        public static void Postfix()
        {
            CustomButton.HudUpdate();
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    public static class MeetingClosePatch
    {
        public static void Postfix()
        {
            CustomButton.MeetingEndedUpdate();
        }
    }
}

// Core Script of PropHuntPlugin
// Copyright (C) 2022  ugackMiner
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor;
using Reactor.Utilities;
using Reactor.Networking.Rpc;
using Reactor.Networking.Attributes;
using UnityEngine;
using System;
using Il2CppInterop.Runtime.Injection;
using AmongUs.GameOptions;
using System.Collections.Generic;
using Reactor.Localization.Utilities;

namespace PropHunt;

[BepInPlugin("com.ugackminer.amongus.prophunt", "Prop Hunt", "v2024.7.21")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public partial class PropHuntPlugin : BasePlugin
{
    // Backend Variables
    public Harmony Harmony { get; } = new("com.ugackminer.amongus.prophunt");
    public ConfigEntry<float> MissTimePenalty { get; private set; }
    public ConfigEntry<bool> Infection { get; private set; }

    // Gameplay Variables
    public static float missTimePenalty = 10f;
    public static bool infection = false;
    public static bool isPropHunt = true;

    public static PropHuntPlugin Instance;

    public override void Load()
    {        
        Instance = PluginSingleton<PropHuntPlugin>.Instance;

        MissTimePenalty = Config.Bind("Prop Hunt", "Miss Penalty", 10f);
        Infection = Config.Bind("Prop Hunt", "Infection", false);

        Harmony.PatchAll(typeof(Patches));
        // Harmony.PatchAll(typeof(CustomRoleSettings));
    }

}
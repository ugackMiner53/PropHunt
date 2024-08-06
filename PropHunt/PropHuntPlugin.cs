// Core Script of PropHuntPlugin
// Copyright (C) 2022  ugackMiner
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq.Expressions;
using Mono.Cecil;
using Reactor;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;
using UnityEngine;

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
    public static bool isPropHunt = true;
    
    // Constants
    public const float propMoveSpeed = 0.5f; 
    public const float maxPropDistance = 0.6f; 

    public static PropHuntPlugin Instance;



    // Debug
    public static Sprite prophuntportraitTest;

    public override void Load()
    {        
        Instance = PluginSingleton<PropHuntPlugin>.Instance;

        MissTimePenalty = Config.Bind("Prop Hunt", "Miss Penalty", 10f);
        Infection = Config.Bind("Prop Hunt", "Infection", false);

        Harmony.PatchAll(typeof(Patches));
        // Harmony.PatchAll(typeof(CustomRoleSettings));

        Texture2D texture = LoadTextureFromPath("PropHunt.Resources.PropHuntPortrait.png");
        prophuntportraitTest = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        prophuntportraitTest.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
    }

    public static unsafe Texture2D LoadTextureFromPath(string path) 
    {
        try {
            Texture2D texture = new(2, 2, TextureFormat.ARGB32, true); //CanvasUtilities.CreateEmptyTexture(2, 2);
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            long length = stream.Length;
            Il2CppStructArray<byte> textureBytes = new Il2CppStructArray<byte>(length);
            stream.Read(new Span<byte>(IntPtr.Add(textureBytes.Pointer, IntPtr.Size * 4).ToPointer(), (int)length));
            ImageConversion.LoadImage(texture, textureBytes, false);
            Logger<PropHuntPlugin>.Info("Correctly loaded " + path);
            return texture;
        } catch {
            Logger<PropHuntPlugin>.Error("Failed loading " + path);
        }
        return null;
    }

}
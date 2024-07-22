using System;
using System.Collections.Generic;
using System.Diagnostics;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.CodeDom;
using InnerNet;
using Reactor.Utilities;

namespace PropHunt;

public class GameModePatches 
{
    

    Dictionary<int, string> englishTranslation = new Dictionary<int, string>{
        {5590401, "Prop Hunt"}
    };

    public static PropHuntGameOptions currentPropHuntGameOptions;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SwitchGameMode))]
    public static bool SwitchGameModePatch(GameOptionsManager __instance, GameModes gameMode) 
    {
        if (currentPropHuntGameOptions == null) {
            currentPropHuntGameOptions = new PropHuntGameOptions(__instance.logger);
        }


        UnityEngine.Debug.Log("SwitchGameMode Patch Called");
        if (gameMode == PropHuntPlugin.PropHuntGameMode) {
            IGameOptions propHuntGameOptions = currentPropHuntGameOptions.Cast<IGameOptions>();
			__instance.currentHostOptions = propHuntGameOptions;
			__instance.currentSearchOptions = propHuntGameOptions;
			__instance.currentGameOptions = propHuntGameOptions;

            __instance.currentGameMode = PropHuntPlugin.PropHuntGameMode;
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HostGame))]
    public static void HostGamePrefix(InnerNetClient __instance, out bool __state, ref IGameOptions settings, GameFilterOptions filterOpts) 
    {
        __state = false;

        if (settings.GameMode == PropHuntPlugin.PropHuntGameMode) {
            UnityEngine.Debug.Log("PROP HUNT GAME IS BEING HOSTED");
            // settings.Cast<PropHuntGameOptions>().GameMode = GameModes.HideNSeek;
            settings = new HideNSeekGameOptionsV08(null).Cast<IGameOptions>();
            Logger<PropHuntPlugin>.Info("Changed game type to hidenseek");
            // UnityEngine.Debug.LogWarning("CHANGED GAME TYPE TO HIDENSEEK NORMAL TO AVOID OOB");
            __state = true;
        }
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManagerCreator), nameof(GameManagerCreator.CreateGameManager))]
    public static bool CreateGameManagerPatch(ref GameManager __result, GameModes mode)
    {
        if (mode != PropHuntPlugin.PropHuntGameMode) {
            UnityEngine.Debug.Log("Not doing prop hunt things because mode is " + mode.ToString());
            return true;
        }

        if (GameManager.Instance != null) {
            UnityEngine.Object.Destroy(GameManager.Instance);
        }

        // This should eventually be the PropHuntManager
        __result = UnityEngine.Object.Instantiate<HideAndSeekManager>(GameManagerCreator.Instance.HideAndSeekManagerPrefab);

        UnityEngine.Debug.Log("Prop Hunt Manager (hnsmanger) instantitated");

        return false;
    }









}
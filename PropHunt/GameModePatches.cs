using System.Collections.Generic;
using System.Diagnostics;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.CodeDom;

namespace PropHunt;

public class GameModePatches 
{

    Dictionary<int, string> englishTranslation = new Dictionary<int, string>{
        {5590401, "Prop Hunt"}
    };

    public static PropHuntGameOptions currentPropHuntGameOptions;


    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.GetString), [typeof(string), typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>)])]
    // public static bool GetStringPatch(ref string __result, string id, string defaultStr, params object[] parts) 
    // {
    //     if (id != "NewRequests") {
    //         Debug.WriteLine(id);
    //     }

    //     if (id == "GameTypePropHunt") {
    //         __result = "My Custom Game";
    //         return false;
    //     }
    //     return true;
    // }

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



}
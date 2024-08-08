using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Reactor.Localization.Utilities;
using Reactor.Utilities;
using UnityEngine;

namespace PropHunt.Settings 
{
    class PropHuntSettings 
    {
        // Prop Hunt Setting
        static StringNames propHuntStringName = CustomStringName.CreateAndRegister("Prop Hunt");
        static BoolOptionNames propHuntBooleanName;
    
        // Time Penalty Setting
        static StringNames timePenaltyStringName = CustomStringName.CreateAndRegister("Miss Penalty");
        static FloatOptionNames timePenaltyFloatName;


        static RulesCategory propHuntCategory;


        // This is run on plugin load and adds the option value names to their respective enums
        public static void SetupCustomSettings() 
        {
            // Add PropHunt option
            propHuntBooleanName = (BoolOptionNames)Enum.GetValues<BoolOptionNames>().Length;
            EnumInjector.InjectEnumValues<BoolOptionNames>(new Dictionary<string, object>{{"PropHunt", propHuntBooleanName}});

            // Add TimePenalty option
            timePenaltyFloatName = (FloatOptionNames)Enum.GetValues<FloatOptionNames>().Length;
            EnumInjector.InjectEnumValues<FloatOptionNames>(new Dictionary<string, object>{{"TimePenalty", timePenaltyFloatName}});
        }

        #region Get/Set Interceptors
        
        // Intercept option requests and change the settings
        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetValue))]
        [HarmonyPostfix]
        static void GetValuePatch(IGameOptions gameOptions, BaseGameSetting data, ref float __result) 
        {
            if (data.Type == OptionTypes.Checkbox && data.TryCast<CheckboxGameSetting>() != null) {
                if (data.Cast<CheckboxGameSetting>().OptionName == propHuntBooleanName) {
                    __result = PropHuntPlugin.isPropHunt ? 1f : 0f;
                }
            } else if (data.Type == OptionTypes.Float && data.TryCast<FloatGameSetting>() != null) {
                if (data.Cast<FloatGameSetting>().OptionName == timePenaltyFloatName) {
                    __result = PropHuntPlugin.missTimePenalty;
                }
            }
        }

        // Overrides booleans being set
        [HarmonyPatch(typeof(HideNSeekGameOptionsV08), nameof(HideNSeekGameOptionsV08.SetBool))]
        [HarmonyPrefix]
        static bool SetBoolPatch(HideNSeekGameOptionsV08 __instance, BoolOptionNames optionName, bool value) 
        {
            if (optionName == propHuntBooleanName) {
                RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, value, PropHuntPlugin.missTimePenalty, PropHuntPlugin.infection);
                return false;
            }
            return true;
        }

        // Overrides floats being set
        [HarmonyPatch(typeof(HideNSeekGameOptionsV08), nameof(HideNSeekGameOptionsV08.SetFloat))]
        [HarmonyPrefix]
        static bool SetFloatPatch(HideNSeekGameOptionsV08 __instance, FloatOptionNames optionName, float value) 
        {
            if (optionName == timePenaltyFloatName) {
                RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, PropHuntPlugin.isPropHunt, value, PropHuntPlugin.infection);
                return false;
            }
            return true;
        }
        #endregion

        // Adds prop hunt settings to HideAndSeekManagerPrefab when GameManagerCreator is started
        [HarmonyPatch(typeof(GameManagerCreator), nameof(GameManagerCreator.Awake))]
        [HarmonyPostfix]
        static void GameManagerCreatorPatch(GameManagerCreator __instance) 
        {
            Il2CppSystem.Collections.Generic.List<BaseGameSetting> allGameList = new Il2CppSystem.Collections.Generic.List<BaseGameSetting>();

            CheckboxGameSetting propHuntCheckbox = ScriptableObject.CreateInstance<CheckboxGameSetting>();
            propHuntCheckbox.Title = propHuntStringName;
            propHuntCheckbox.OptionName = propHuntBooleanName;
            propHuntCheckbox.Type = OptionTypes.Checkbox;
            propHuntCheckbox.name = "Prop Hunt";
            allGameList.System_Collections_IList_Add(propHuntCheckbox);

            FloatGameSetting timePenaltyFloat = ScriptableObject.CreateInstance<FloatGameSetting>();
            timePenaltyFloat.Title = timePenaltyStringName;
            timePenaltyFloat.OptionName = timePenaltyFloatName;
            timePenaltyFloat.Type = OptionTypes.Float;
            timePenaltyFloat.name = "Time Penalty";
            timePenaltyFloat.Increment = 5;
            timePenaltyFloat.FormatString = "0.0#";
            timePenaltyFloat.SuffixType = NumberSuffixes.Seconds;
            timePenaltyFloat.ZeroIsInfinity = false;
            timePenaltyFloat.ValidRange = new FloatRange(0, 60);
            timePenaltyFloat.Value = PropHuntPlugin.missTimePenalty;
            allGameList.System_Collections_IList_Add(timePenaltyFloat);


            propHuntCategory = new RulesCategory
            {
                AllGameSettings = allGameList,
                CategoryName = propHuntStringName
            };

            __instance.HideAndSeekManagerPrefab.gameSettingsList.AllCategories.System_Collections_IList_Add(propHuntCategory);
        }
    }
}
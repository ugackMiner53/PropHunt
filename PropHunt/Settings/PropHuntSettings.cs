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
        static StringNames propHuntStringName = CustomStringName.CreateAndRegister("Prop Hunt");
        static BoolOptionNames propHuntBooleanName;
        static RulesCategory propHuntCategory;


        // This is run on plugin load and adds the option value names to their respective enums
        public static void SetupCustomSettings() 
        {
            // Add PropHunt settings category
            propHuntBooleanName = (BoolOptionNames)Enum.GetValues<BoolOptionNames>().Length;
            EnumInjector.InjectEnumValues<BoolOptionNames>(new Dictionary<string, object>{{"PropHunt", propHuntBooleanName}});
        }


        // Overrides booleans being get
        [HarmonyPatch(typeof(HideNSeekGameOptionsV08), nameof(HideNSeekGameOptionsV08.TryGetBool))]
        [HarmonyPostfix]
        static void TryGetBoolPatch(BoolOptionNames optionName, ref bool value)
        {
            if (optionName == propHuntBooleanName) {
                value = PropHuntPlugin.isPropHunt;
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


        // Adds prop hunt settings to HideAndSeekManagerPrefab when GameManagerCreator is started
        [HarmonyPatch(typeof(GameManagerCreator), nameof(GameManagerCreator.Awake))]
        [HarmonyPostfix]
        static void GameManagerCreatorPatch(GameManagerCreator __instance) 
        {
            Il2CppSystem.Collections.Generic.List<BaseGameSetting> allGameList = new Il2CppSystem.Collections.Generic.List<BaseGameSetting>();

            Logger<PropHuntPlugin>.Info("Adding to GameManagerCreator. THIS SHOULD ONLY RUN ONCE");

            CheckboxGameSetting checkboxSetting = ScriptableObject.CreateInstance<CheckboxGameSetting>();
            checkboxSetting.Title = propHuntStringName;
            checkboxSetting.OptionName = propHuntBooleanName;
            checkboxSetting.Type = OptionTypes.Checkbox;
            checkboxSetting.name = "Prop Hunt";
            allGameList.System_Collections_IList_Add(checkboxSetting);

            propHuntCategory = new RulesCategory
            {
                AllGameSettings = allGameList,
                CategoryName = propHuntStringName
            };

            __instance.HideAndSeekManagerPrefab.gameSettingsList.AllCategories.System_Collections_IList_Add(propHuntCategory);
        }
    }
}
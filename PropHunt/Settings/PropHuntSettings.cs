using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Reactor.Localization.Utilities;
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
			EnumInjector.InjectEnumValues<BoolOptionNames>(new Dictionary<string, object> { { "PropHunt", propHuntBooleanName } });

			// Add TimePenalty option
			timePenaltyFloatName = (FloatOptionNames)Enum.GetValues<FloatOptionNames>().Length;
			EnumInjector.InjectEnumValues<FloatOptionNames>(new Dictionary<string, object> { { "TimePenalty", timePenaltyFloatName } });
		}

		#region Get/Set Interceptors

		// Intercept option requests and change the settings
		[HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetValue))]
		[HarmonyPostfix]
		static void GetValuePatch(IGameOptions gameOptions, BaseGameSetting data, ref float __result)
		{
			if (data.Type == OptionTypes.Checkbox && data.TryCast<CheckboxGameSetting>() != null)
			{
				if (data.Cast<CheckboxGameSetting>().OptionName == propHuntBooleanName)
				{
					__result = PropHuntPlugin.isPropHunt ? 1f : 0f;
				}
			}
			else if (data.Type == OptionTypes.Float && data.TryCast<FloatGameSetting>() != null)
			{
				if (data.Cast<FloatGameSetting>().OptionName == timePenaltyFloatName)
				{
					__result = PropHuntPlugin.missTimePenalty;
				}
			}
		}

		// Overrides booleans being set
		[HarmonyPatch(typeof(HideNSeekGameOptionsV10), nameof(HideNSeekGameOptionsV10.SetBool))]
		[HarmonyPrefix]
		static bool SetBoolPatch(HideNSeekGameOptionsV10 __instance, BoolOptionNames optionName, bool value)
		{
			if (optionName == propHuntBooleanName)
			{
				RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, value, PropHuntPlugin.missTimePenalty, PropHuntPlugin.infection);
				return false;
			}
			return true;
		}

		// Overrides floats being set
		[HarmonyPatch(typeof(HideNSeekGameOptionsV10), nameof(HideNSeekGameOptionsV10.SetFloat))]
		[HarmonyPrefix]
		static bool SetFloatPatch(HideNSeekGameOptionsV10 __instance, FloatOptionNames optionName, float value)
		{
			if (optionName == timePenaltyFloatName)
			{
				RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, PropHuntPlugin.isPropHunt, value, PropHuntPlugin.infection);
				return false;
			}
			return true;
		}
		#endregion

		// Tracks whether settings have already been injected into the prefab
		static bool settingsInjected = false;

		// Adds prop hunt settings to HideAndSeekManagerPrefab when GameManagerCreator is started.
		// Uses a static flag to prevent duplicate entries when players re-enter a room.
		[HarmonyPatch(typeof(GameManagerCreator), nameof(GameManagerCreator.Awake))]
		[HarmonyPostfix]
		static void GameManagerCreatorPatch(GameManagerCreator __instance)
		{
			// Guard against duplicate injection across scene reloads / room re-entries
			if (settingsInjected) return;
			settingsInjected = true;

			Il2CppSystem.Collections.Generic.List<BaseGameSetting> allGameList = new Il2CppSystem.Collections.Generic.List<BaseGameSetting>();

			CheckboxGameSetting propHuntCheckbox = ScriptableObject.CreateInstance<CheckboxGameSetting>();
			propHuntCheckbox.Title = propHuntStringName;
			propHuntCheckbox.OptionName = propHuntBooleanName;
			propHuntCheckbox.Type = OptionTypes.Checkbox;
			propHuntCheckbox.name = "Prop Hunt";
			allGameList.Add(propHuntCheckbox);

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
			allGameList.Add(timePenaltyFloat);

			propHuntCategory = new RulesCategory
			{
				AllGameSettings = allGameList,
				CategoryName = propHuntStringName
			};

			// This is marked as dynamic because we need System_Collections_IList_Add for the settings
			// to actually be added, but it only exists at runtime for some reason.
			dynamic AllCategories = __instance.HideAndSeekManagerPrefab.gameSettingsList.AllCategories;
			AllCategories.System_Collections_IList_Add(propHuntCategory);
		}

		// Fixes the view-only lobby settings pane scroll bounds so the Prop Hunt
		// category is fully scrollable and not cut off at the bottom.
		//
		// DrawNormalTab calls CalculateAndSetYBounds which computes scroll height from item count,
		// but counts all items equally at 0.85f spacing ¡ª it does not account for the extra height
		// of category headers (1.05f each). The PropHunt category adds:
		//   1 header  = 1.05f
		//   2 items laid out 2-per-row = 1 row = 0.85f
		//   trailing gap after loop   = 0.85f
		//   total extra               = 2.75f
		//
		// We read the max that DrawNormalTab already set and extend it by exactly that amount.
		// Using SetYBoundsMax avoids re-deriving everything from item count and losing header height.
		[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.ChangeTab))]
		[HarmonyPostfix]
		static void LobbyViewSettingsPaneChangeTabPatch(LobbyViewSettingsPane __instance)
		{
			// header(1.05) + 1 item-row(0.85) + trailing gap(0.85) = 2.75
			const float propHuntCategoryHeight = 1.05f + 0.85f + 0.85f;
			__instance.scrollBar.SetYBoundsMax(
				__instance.scrollBar.ContentYBounds.max + propHuntCategoryHeight);
		}
	}
}
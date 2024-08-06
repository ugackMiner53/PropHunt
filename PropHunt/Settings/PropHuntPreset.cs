using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Reactor.Localization.Utilities;
using Reactor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PropHunt.Settings
{
    class PropHuntPreset 
    {
        static StringNames presetStringName = CustomStringName.CreateAndRegister("Prop Hunt");
        const string propHuntDescription = "The preset for the ideal Prop Hunt experience with balanced game settings.";
        static RulesPresets propHuntRulePreset;
        static Sprite presetPortraitSprite;
        static PassiveButton presetButton;


        // Called on plugin load to setup prerequisites
        public static void SetupPreset() 
        {
            // Add rule preset
            propHuntRulePreset = (RulesPresets)Enum.GetValues<RulesPresets>().Length;
            EnumInjector.InjectEnumValues<RulesPresets>(new Dictionary<string, object>{{"PropHunt", propHuntRulePreset}});

            // Load Preset texture
            Texture2D texture = Utility.LoadTextureFromPath("PropHunt.Resources.PropHuntPortrait.png");
            presetPortraitSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            presetPortraitSprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
        }


        // Build preset button
        [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.Start))]
        [HarmonyPostfix]
        static void CreatePropHuntPreset(GamePresetsTab __instance)
        {
            if (GameOptionsManager.Instance.currentGameMode != GameModes.HideNSeek) return;

            GameObject propHuntObject = GameObject.Instantiate(__instance.SecondPresetButton.gameObject);
            propHuntObject.name = "PropHuntButton";
            propHuntObject.transform.SetParent(__instance.transform);

            // Setup the correct components (they don't copy correctly)
            presetButton = propHuntObject.GetComponent<PassiveButton>();
            presetButton.buttonText = presetButton.transform.GetChild(0).GetComponent<TextMeshPro>();
            presetButton.activeSprites = propHuntObject.transform.GetChild(1).gameObject;
            presetButton.inactiveSprites = propHuntObject.transform.GetChild(2).gameObject;
            presetButton.selectedSprites = propHuntObject.transform.GetChild(3).gameObject;

            // Change button text
            presetButton.buttonText.text = "Prop Hunt";
            presetButton.selectedTextColor = Color.white;

            // Set the sprite of the button
            presetButton.activeSprites.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite = presetPortraitSprite;
            presetButton.inactiveSprites.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite = presetPortraitSprite;
            presetButton.selectedSprites.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite = presetPortraitSprite;

            // Only enable the correct sprites
            if (GameOptionsManager.Instance.CurrentGameOptions.RulesPreset == propHuntRulePreset) {
                __instance.StandardPresetButton.SelectButton(false);
                __instance.SecondPresetButton.SelectButton(false);
                presetButton.SelectButton(true);
            } else {
                presetButton.SelectButton(false);
            }

            // Resize & Reposition all of the buttons
            Vector3 smallerScale = new Vector3(0.75f, 1, 1);
            __instance.StandardPresetButton.transform.localScale = smallerScale;
            __instance.SecondPresetButton.transform.localScale = smallerScale;
            presetButton.transform.localScale = smallerScale;

            __instance.StandardPresetButton.transform.localPosition = new Vector3(-2, -0.1f, 0);
            __instance.SecondPresetButton.transform.localPosition = new Vector3(0.2f, -0.1f, 0);
            presetButton.transform.localPosition = new Vector3(2.4f, -0.1f, 0);

            // Add interactivity to the prop button
            presetButton.OnClick.AddListener((UnityAction)delegate {
                __instance.StandardPresetButton.SelectButton(false);
                __instance.SecondPresetButton.SelectButton(false);
                presetButton.SelectButton(true);
                __instance.ClickPresetButton(propHuntRulePreset);
            });

            presetButton.OnMouseOver.AddListener((UnityAction)delegate {
                __instance.PresetDescriptionText.text = propHuntDescription;
            });

            presetButton.OnMouseOut.AddListener((UnityAction)delegate {
                __instance.SetSelectedText();
            });

            // Reset Prop Hunt button when other buttons clicked
            void ResetPropHunt() {
                presetButton.SelectButton(false);
            }
            __instance.StandardPresetButton.OnClick.AddListener((UnityAction)ResetPropHunt);
            __instance.SecondPresetButton.OnClick.AddListener((UnityAction)ResetPropHunt);
        }


        // Keep the correct preset when switching between tabs
        [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.OnEnable))]
        [HarmonyPostfix]
        static void GamePresetsEnablePatch(GamePresetsTab __instance)
        {
            if (!presetButton) return;

            if (GameOptionsManager.Instance.CurrentGameOptions.RulesPreset == propHuntRulePreset) {
                __instance.StandardPresetButton.SelectButton(false);
                __instance.SecondPresetButton.SelectButton(false);
                presetButton.SelectButton(true);
            } else {
                presetButton.SelectButton(false);
            }
        }


        // Show correct text when selected
        [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.SetSelectedText))]
        [HarmonyPostfix]
        static void GamePresetsSelectedTextPatch(GamePresetsTab __instance) 
        {
            if (GameOptionsManager.Instance.CurrentGameOptions.RulesPreset == propHuntRulePreset) 
            {
                __instance.PresetDescriptionText.text = propHuntDescription;
            }
        }


        // Set the correct recommendations when the prop preset is selected
        [HarmonyPatch(typeof(HideNSeekGameOptionsV08), nameof(HideNSeekGameOptionsV08.SetRecommendations), [typeof(int), typeof(bool), typeof(RulesPresets)])]
        [HarmonyPostfix]
        public static void SetRecommendations(HideNSeekGameOptionsV08 __instance, int numPlayers, bool isOnline, RulesPresets rulesPresets) 
        {
            if (rulesPresets == propHuntRulePreset) 
            {
                /* Recommended Settings:
                *   - Final Seek Pings & Map
                *   - Flashlight off
                *   - Lower Final Time
                *   - Longer Hiding Time
                *   - Larger impostor vision radius
                */

                __instance.SeekerPings = false;
                __instance.SeekerFinalMap = false;
                __instance.FinalCountdownTime = 30f;
                __instance.EscapeTime = 240f;
                RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, true, 10f, false);
            } else {
                RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, false, 10f, false);
            }
        }


        // Set the correct name in the preset panel
        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetRulesPresetTitle))]
        [HarmonyPostfix]
        static void GetRulesPresetTitlePatch(IGameOptions gameOptions, ref StringNames __result)
        {
            if (gameOptions.RulesPreset == propHuntRulePreset) 
            {
                __result = presetStringName;
            }
        }

    }
}
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PropHunt.Settings
{
    [HarmonyPatch]
    public static class PropHuntSettingsMenu
    {
        private const int MaskLayer = 20;
        private const float OptionX = 0.952f;
        private const float OptionSpacing = 0.45f;
        private const float HeaderX = -0.903f;
        private const float HeaderScale = 0.63f;
        private const float HeaderSpacing = 0.63f;
        private const string HeaderName = "PropHuntHeader";
        private const string OptionNamePrefix = "PropHuntOption";

        private const int ViewMaskLayer = 61;
        private const float ViewHeaderStartX = -9.77f;
        private const float ViewHeaderSpacing = 1.05f;
        private const float ViewRowSpacing = 0.85f;
        private const float ViewLeftX = -8.95f;
        private const float ViewRightX = -3f;
        private const string ViewHeaderName = "PropHuntViewHeader";
        private const string ViewPanelNamePrefix = "PropHuntViewPanel";

        #region GameOptionsMenu (host editable settings)

        [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.OnEnable))]
        [HarmonyPostfix]
        private static void GameOptionsMenuOnEnable(GameOptionsMenu __instance)
        {
            if (!PropHuntOptions.IsHideNSeek()) return;
            // CreateSettings (called inside OnEnable) already appended our rows.
            if (HasAppendedOptions(__instance.settingsContainer)) return;
            AppendOptions(__instance);
        }

        [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.CreateSettings))]
        [HarmonyPostfix]
        private static void GameOptionsMenuCreateSettings(GameOptionsMenu __instance)
        {
            if (!PropHuntOptions.IsHideNSeek()) return;
            AppendOptions(__instance);
        }

        private static void AppendOptions(GameOptionsMenu menu)
        {
            try
            {
                Transform container = menu.settingsContainer;
                Scroller scrollBar = menu.scrollBar;
                if (container == null || scrollBar == null) return;
                if (menu.stringOptionOrigin == null) return;

                // Remove any previously appended PropHunt rows so we can re-place
                // them correctly after a vanilla rebuild (option count can change).
                ClearAppendedChildren(container);

                // Bottom-most vanilla child = where our section starts below it.
                float bottomY = 0.713f;
                bool found = false;
                for (int i = 0; i < container.childCount; i++)
                {
                    Transform child = container.GetChild(i);
                    if (child == null || !child.gameObject.activeSelf) continue;
                    float childY = child.localPosition.y;
                    if (!found || childY < bottomY) { bottomY = childY; found = true; }
                }
                if (!found) return;
                float startY = bottomY;

                float y = startY - HeaderSpacing;

                // "Prop Hunt" category header
                if (menu.categoryHeaderOrigin != null)
                {
                    CategoryHeaderMasked header = Object.Instantiate(menu.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, container);
                    header.gameObject.name = HeaderName;
                    header.transform.localScale = Vector3.one * HeaderScale;
                    header.transform.localPosition = new Vector3(HeaderX, y, -2f);
                    header.SetHeader(StringNames.GameMapName, MaskLayer);
                    if (header.Title != null) header.Title.text = "Prop Hunt";
                    y -= HeaderSpacing;
                }

                // Custom option rows
                for (int i = 0; i < PropHuntOptions.AllOption.Count; i++)
                {
                    PropHuntOption classOption = PropHuntOptions.AllOption[i];
                    StringOption option = Object.Instantiate(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, container);
                    option.gameObject.name = OptionNamePrefix + classOption.Id;
                    option.transform.localPosition = new Vector3(OptionX, y, -2f);
                    option.SetClickMask(menu.ButtonClickMask);
                    ApplyMask(option);
                    option.Values = new StringNames[classOption.AllValues.Length];
                    option.Value = classOption.Value;
                    if (option.TitleText != null) option.TitleText.text = classOption.Name;
                    if (option.ValueText != null) option.ValueText.text = classOption.AllValues[classOption.Value] + classOption.Suffix;

                    if (classOption.AllValues.Length <= 1)
                    {
                        if (option.PlusBtn != null) option.PlusBtn.SetInteractable(false);
                        if (option.MinusBtn != null) option.MinusBtn.SetInteractable(false);
                    }
                    else
                    {
                        RefreshButtonState(option);
                    }

                    if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) option.SetAsPlayer();
                    y -= OptionSpacing;
                }

                // Extend the scroll bounds by the appended height so nothing is cut off.
                float appendedHeight = (startY - y) + OptionSpacing;
                scrollBar.SetYBoundsMax(scrollBar.ContentYBounds.max + appendedHeight);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[PropHunt] AppendOptions failed: " + e);
            }
        }

        private static bool HasAppendedOptions(Transform container)
        {
            if (container == null) return false;
            for (int i = 0; i < container.childCount; i++)
            {
                if (container.GetChild(i).name == HeaderName) return true;
            }
            return false;
        }

        private static void ClearAppendedChildren(Transform container)
        {
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < container.childCount; i++)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (child == null) continue;
                if (child.name == HeaderName || child.name.StartsWith(OptionNamePrefix))
                {
                    toDestroy.Add(child);
                }
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                if (toDestroy[i] != null) Object.DestroyImmediate(toDestroy[i]);
            }
        }

        private static void ApplyMask(StringOption option)
        {
            SpriteRenderer[] sprites = option.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i].material.SetInt(PlayerMaterial.MaskLayer, MaskLayer);
            }
            TextMeshPro[] texts = option.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].fontMaterial.SetFloat("_StencilComp", 3f);
                texts[i].fontMaterial.SetFloat("_Stencil", MaskLayer);
            }
        }

        private static void RefreshButtonState(StringOption option)
        {
            AccessTools.Method(typeof(StringOption), "AdjustButtonsActiveState")?.Invoke(option, null);
        }

        private static PropHuntOption FindClassOption(StringOption option)
        {
            if (option == null || !option.name.StartsWith(OptionNamePrefix)) return null;
            if (!byte.TryParse(option.name.Substring(OptionNamePrefix.Length), out byte id)) return null;
            return PropHuntOptions.Find(id);
        }

        #endregion

        #region LobbyViewSettingsPane (read-only view)

        [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.ChangeTab))]
        [HarmonyPostfix]
        private static void LobbyViewSettingsChangeTab(LobbyViewSettingsPane __instance)
        {
            if (!PropHuntOptions.IsHideNSeek()) return;
            AppendViewSettings(__instance);
            const float propHuntCategoryHeight = 1.05f + 0.85f + 0.85f;
            __instance.scrollBar.SetYBoundsMax(__instance.scrollBar.ContentYBounds.max + propHuntCategoryHeight);
        }

        [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.SetTab))]
        [HarmonyPostfix]
        private static void LobbyViewSettingsSetTab(LobbyViewSettingsPane __instance)
        {
            if (!PropHuntOptions.IsHideNSeek()) return;
            AppendViewSettings(__instance);
        }

        [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.RefreshTab))]
        [HarmonyPostfix]
        private static void LobbyViewSettingsRefreshTab(LobbyViewSettingsPane __instance)
        {
            if (!PropHuntOptions.IsHideNSeek()) return;
            AppendViewSettings(__instance);
        }

        private static void AppendViewSettings(LobbyViewSettingsPane pane)
        {
            try
            {
                Transform container = pane.settingsContainer;
                if (container == null) return;
                if (pane.infoPanelOrigin == null) return;

                // Only append on the normal settings tab, never the Roles tab
                // (roles panels are built from a different origin).
                for (int i = 0; i < container.childCount; i++)
                {
                    if (container.GetChild(i).GetComponent<CategoryHeaderRoleVariant>() != null) return;
                }

                // Remove any previously appended PropHunt view content.
                ClearViewChildren(pane, container);

                float bottomY = 1.44f;
                bool found = false;
                for (int i = 0; i < container.childCount; i++)
                {
                    Transform child = container.GetChild(i);
                    if (child == null || !child.gameObject.activeSelf) continue;
                    float childY = child.localPosition.y;
                    if (!found || childY < bottomY) { bottomY = childY; found = true; }
                }
                if (!found) return;

                float y = bottomY - ViewRowSpacing;

                // Header
                if (pane.categoryHeaderOrigin != null)
                {
                    CategoryHeaderMasked header = Object.Instantiate(pane.categoryHeaderOrigin);
                    header.gameObject.name = ViewHeaderName;
                    header.transform.SetParent(container, false);
                    header.transform.localScale = Vector3.one;
                    header.transform.localPosition = new Vector3(ViewHeaderStartX, y, -2f);
                    header.SetHeader(StringNames.GameMapName, ViewMaskLayer);
                    if (header.Title != null) header.Title.text = "Prop Hunt";
                    if (pane.settingsInfo != null) pane.settingsInfo.Add(header.gameObject);
                    y -= ViewHeaderSpacing;
                }

                // Two-column panels (like the vanilla layout)
                for (int i = 0; i < PropHuntOptions.AllOption.Count; i++)
                {
                    PropHuntOption classOption = PropHuntOptions.AllOption[i];
                    ViewSettingsInfoPanel panel = Object.Instantiate(pane.infoPanelOrigin);
                    panel.gameObject.name = ViewPanelNamePrefix + classOption.Id;
                    panel.transform.SetParent(container, false);
                    panel.transform.localScale = Vector3.one;
                    float x;
                    if (i % 2 == 0)
                    {
                        x = ViewLeftX;
                        if (i > 0) y -= ViewRowSpacing;
                    }
                    else
                    {
                        x = ViewRightX;
                    }
                    panel.transform.localPosition = new Vector3(x, y, -2f);
                    panel.SetInfo((StringNames)0, classOption.AllValues[classOption.Value] + classOption.Suffix, ViewMaskLayer);
                    if (panel.titleText != null) panel.titleText.text = classOption.Name;
                    if (pane.settingsInfo != null) pane.settingsInfo.Add(panel.gameObject);
                }
                y -= ViewRowSpacing;

                if (pane.scrollBar != null && pane.settingsInfo != null)
                {
                    pane.scrollBar.CalculateAndSetYBounds((float)(pane.settingsInfo.Count + 10), 2f, 6f, ViewRowSpacing);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[PropHunt] AppendViewSettings failed: " + e);
            }
        }

        private static void ClearViewChildren(LobbyViewSettingsPane pane, Transform container)
        {
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < container.childCount; i++)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (child == null) continue;
                if (child.name == ViewHeaderName || child.name.StartsWith(ViewPanelNamePrefix))
                {
                    if (pane.settingsInfo != null) pane.settingsInfo.Remove(child);
                    toDestroy.Add(child);
                }
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                if (toDestroy[i] != null) Object.DestroyImmediate(toDestroy[i]);
            }
        }

        #endregion

        #region StringOption (custom row behavior)

        [HarmonyPatch(typeof(StringOption), nameof(StringOption.Start))]
        [HarmonyPrefix]
        private static bool StringOptionStart(StringOption __instance)
        {
            // Our options have no BaseGameSetting data, so skip the vanilla
            // Initialize for them (it would read a null option data).
            return FindClassOption(__instance) == null;
        }

        [HarmonyPatch(typeof(StringOption), nameof(StringOption.FixedUpdate))]
        [HarmonyPrefix]
        private static bool StringOptionFixedUpdate(StringOption __instance)
        {
            // Skip the vanilla text refresh for our options (we set it manually).
            return FindClassOption(__instance) == null;
        }

        [HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
        [HarmonyPrefix]
        private static bool StringOptionIncrease(StringOption __instance)
        {
            PropHuntOption classOption = FindClassOption(__instance);
            if (classOption == null) return true;
            if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return false;
            if (classOption.Value < classOption.AllValues.Length - 1)
            {
                classOption.Value++;
                __instance.Value = classOption.Value;
                __instance.ValueText.text = classOption.AllValues[classOption.Value] + classOption.Suffix;
                RefreshButtonState(__instance);
                PropHuntOptions.RpcPushDelta(classOption.Id, classOption.Value);
            }
            return false;
        }

        [HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
        [HarmonyPrefix]
        private static bool StringOptionDecrease(StringOption __instance)
        {
            PropHuntOption classOption = FindClassOption(__instance);
            if (classOption == null) return true;
            if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return false;
            if (classOption.Value > 0)
            {
                classOption.Value--;
                __instance.Value = classOption.Value;
                __instance.ValueText.text = classOption.AllValues[classOption.Value] + classOption.Suffix;
                RefreshButtonState(__instance);
                PropHuntOptions.RpcPushDelta(classOption.Id, classOption.Value);
            }
            return false;
        }

        #endregion
    }
}

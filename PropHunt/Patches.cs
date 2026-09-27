using AmongUs.GameOptions;
using HarmonyLib;
using PowerTools;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;
using UnityEngine;

namespace PropHunt
{
    [HarmonyPatch]
    public class Patches
    {
        private static CustomButton disguiseButton;
        private static CustomButton revertButton;
        private static CustomButton movePropButton;
        public static bool isMovingProp = false;

        private static GameObject propPreviewHolder;
        private static SpriteRenderer propPreviewRenderer;

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
        [HarmonyPostfix]
        public static void HudManagerStartPatch(HudManager __instance)
        {
            disguiseButton = new CustomButton(
                () =>
                {
                    if (!PropHuntPlugin.isPropHunt || PlayerControl.LocalPlayer.Data.Role.IsImpostor) return;
                    Console closest = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, PropHuntPlugin.disguiseRange);
                    if (closest != null)
                    {
                        for (int i = 0; i < ShipStatus.Instance.AllConsoles.Length; i++)
                        {
                            if (ShipStatus.Instance.AllConsoles[i] == closest)
                            {
                                Logger<PropHuntPlugin>.Info("Task of index " + i + " being sent out");
                                RPCHandler.RPCPropSync(PlayerControl.LocalPlayer, i + "");
                                // Start the disguise cooldown (configurable in the settings)
                                disguiseButton.Timer = Mathf.Max(0.01f, PropHuntPlugin.disguiseCooldown);
                                disguiseButton.MaxTimer = Mathf.Max(0.01f, PropHuntPlugin.disguiseCooldown);
                                break;
                            }
                        }
                    }
                },
                () => PropHuntPlugin.isPropHunt
                       && !PlayerControl.LocalPlayer.Data.Role.IsImpostor
                       && !PlayerControl.LocalPlayer.Data.IsDead
                       && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
                () =>
                {
                    if (!PropHuntPlugin.isPropHunt || PlayerControl.LocalPlayer.Data.Role.IsImpostor
                        || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
                        return false;
                    Console target = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, PropHuntPlugin.disguiseRange);
                    if (target != null)
                    {
                        Sprite s = target.GetComponent<SpriteRenderer>()?.sprite
                                ?? target.GetComponentInChildren<SpriteRenderer>()?.sprite;
                        if (s != null)
                        {
                            propPreviewRenderer.sprite = s;
                            propPreviewHolder.transform.localScale = Vector3.one;
                            float max = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
                            if (max > 0) propPreviewHolder.transform.localScale /= max;
                            return true;
                        }
                    }
                    // No prop in range → clear the stale preview so it doesn't linger
                    if (propPreviewRenderer != null) propPreviewRenderer.sprite = null;
                    return false;
                },
                () => { },
                null,
                new Vector3(0f, 1f, 0f),
                __instance,
                KeyCode.R,
                buttonText: "DISGUISE"
            );

            propPreviewHolder = new GameObject("PropPreview");
            propPreviewRenderer = propPreviewHolder.AddComponent<SpriteRenderer>();
            propPreviewHolder.transform.SetParent(disguiseButton.actionButton.transform, false);
            propPreviewHolder.transform.localPosition = new Vector3(0, 0, -2f);

            // Ready to disguise immediately (override the button's default cooldown value)
            disguiseButton.Timer = -1f;

            revertButton = new CustomButton(
                () =>
                {
                    if (!PropHuntPlugin.isPropHunt || PlayerControl.LocalPlayer.Data.Role.IsImpostor) return;
                    PlayerControl player = PlayerControl.LocalPlayer;
                    if (TryGetAliveProp(player, out SpriteRenderer revertProp) && revertProp.sprite != null)
                    {
                        Logger<PropHuntPlugin>.Info("Reverting to crewmate");
                        RPCHandler.RPCRevert(player);
                        player.Visible = true;

                        // Reverting the disguise also releases the "move prop"
                        // lock so the player regains normal movement control.
                        if (isMovingProp)
                        {
                            isMovingProp = false;
                            if (movePropButton != null)
                            {
                                movePropButton.buttonText = "MOVE PROP";
                                movePropButton.actionButtonRenderer.color = Palette.EnabledColor;
                                movePropButton.Timer = 0f;
                                movePropButton.isEffectActive = false;
                            }
                        }
                    }
                },
                () => PropHuntPlugin.isPropHunt
                       && !PlayerControl.LocalPlayer.Data.Role.IsImpostor
                       && !PlayerControl.LocalPlayer.Data.IsDead
                       && TryGetAliveProp(PlayerControl.LocalPlayer, out SpriteRenderer revertProp) && revertProp.sprite != null
                       && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
                () => true,
                () => { },
                Utility.LoadSprite("PropHunt.Resources.RevertButton.png", 150f),
                new Vector3(-2f, 1f, 0f),
                __instance,
                KeyCode.C,
                buttonText: "REVERT"
            );
            revertButton.Timer = -1f;

            movePropButton = new CustomButton(
                () =>
                {
                    if (isMovingProp)
                    {
                        PlayerControl player = PlayerControl.LocalPlayer;
                        if (PropManager.playerToProp.ContainsKey(player))
                        {
                            // Sync the final (fine-tuned) position to every client
                            RPCHandler.RPCPropPos(player, PropManager.playerToProp[player].transform.localPosition);
                        }
                        isMovingProp = false;
                        movePropButton.buttonText = "MOVE PROP";
                        movePropButton.actionButtonRenderer.color = Palette.EnabledColor;
                        movePropButton.Timer = 0f;
                        movePropButton.isEffectActive = false;
                    }
                    else
                    {
                        isMovingProp = true;
                        movePropButton.buttonText = "FIX PROP";
                        movePropButton.actionButtonRenderer.color = new Color(0F, 0.8F, 0F);
                    }
                },
                () => PropHuntPlugin.isPropHunt
                       && !PlayerControl.LocalPlayer.Data.Role.IsImpostor
                       && !PlayerControl.LocalPlayer.Data.IsDead
                       && TryGetAliveProp(PlayerControl.LocalPlayer, out SpriteRenderer moveCheckProp) && moveCheckProp.sprite != null
                       && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started,
                () => true,
                () =>
                {
                    if (isMovingProp)
                    {
                        PlayerControl player = PlayerControl.LocalPlayer;
                        if (PropManager.playerToProp.ContainsKey(player))
                        {
                            RPCHandler.RPCPropPos(player, PropManager.playerToProp[player].transform.localPosition);
                        }
                        isMovingProp = false;
                        movePropButton.buttonText = "MOVE PROP";
                    }
                },
                Utility.LoadSprite("PropHunt.Resources.MovePropButton.png", 150f),
                new Vector3(-1f, 1f, 0f),
                __instance,
                KeyCode.LeftShift,
                hasEffect: false,
                effectDuration: 0f,
                onEffectEnds: () => { },
                buttonText: "MOVE PROP"
            );
            movePropButton.Timer = -1f;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
        [HarmonyPostfix]
        public static void MovePropPatch(PlayerPhysics __instance)
        {
            if (!PropHuntPlugin.isPropHunt || !__instance.AmOwner) return;
            if (!isMovingProp || !TryGetAliveProp(__instance.myPlayer, out SpriteRenderer movePropRenderer)) return;

            Vector2 input = DestroyableSingleton<HudManager>.Instance.joystick.DeltaL;

            Transform prop = movePropRenderer.transform;
            Vector3 newPosition = new Vector3(
                prop.localPosition.x + input.x * PropHuntPlugin.propMoveSpeed * Time.fixedDeltaTime,
                prop.localPosition.y + input.y * PropHuntPlugin.propMoveSpeed * Time.fixedDeltaTime,
                -3);

            if (Vector2.Distance(Vector2.zero, newPosition) < PropHuntPlugin.maxPropDistance)
            {
                prop.localPosition = newPosition;
            }
            __instance.SetNormalizedVelocity(Vector2.zero);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
        [HarmonyPostfix]
        public static void PlayerControlStartPatch(PlayerControl __instance)
        {
            GameObject propObj = new GameObject("Prop")
            {
                layer = 11
            };
            SpriteRenderer propRenderer = propObj.AddComponent<SpriteRenderer>();
            propObj.transform.SetParent(__instance.transform);
            propObj.transform.localScale = Vector2.one;
            propObj.transform.localPosition = new Vector3(0, 0, -3);
            PropManager.playerToProp.Add(__instance, propRenderer);
        }

        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
        [HarmonyPostfix]
        public static void OnExitGame()
        {
            PropManager.playerToProp.Clear();
            isMovingProp = false;
        }

        [HarmonyPatch(typeof(LogicOptionsHnS), nameof(LogicOptionsHnS.GetCrewmateLeadTime))]
        [HarmonyPostfix]
        public static void CrewmateLeadTimePatch(ref int __result)
        {
            if (!PropHuntPlugin.isPropHunt) return;
            __result = (int)PropHuntPlugin.seekerWaitTime;
        }

        [HarmonyPatch(typeof(PlayerControl), "set_Visible")]
        [HarmonyPrefix]
        public static void PlayerVisibleGuardPatch(PlayerControl __instance, ref bool value)
        {
            if (!value || !PropHuntPlugin.isPropHunt) return;
            if (!TryGetAliveProp(__instance, out SpriteRenderer guardProp) || guardProp.sprite == null) return;
            if (__instance.Data == null || __instance.Data.Role == null) return;
            if (__instance.Data.Role.IsImpostor || __instance.Data.IsDead) return;
            value = false;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
        [HarmonyPostfix]
        public static void PropVentVisibilityPatch(PlayerPhysics __instance)
        {
            if (!PropHuntPlugin.isPropHunt || __instance.myPlayer == null) return;
            if (!TryGetAliveProp(__instance.myPlayer, out SpriteRenderer ventProp) || ventProp.sprite == null) return;
            bool shouldShow = !__instance.myPlayer.inVent;
            if (ventProp.enabled != shouldShow) ventProp.enabled = shouldShow;
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
        [HarmonyPostfix]
        public static void OnPlayerDiePatch(PlayerControl __instance)
        {
            if (!PropHuntPlugin.isPropHunt || __instance.Data.Role.IsImpostor) return;

            if (PropManager.playerToProp.TryGetValue(__instance, out SpriteRenderer prop))
            {
                if (prop != null)
                {
                    prop.gameObject.Destroy();
                }
                PropManager.playerToProp.Remove(__instance);
            }
            if (__instance == PlayerControl.LocalPlayer)
            {
                isMovingProp = false;
                if (movePropButton != null)
                {
                    movePropButton.actionButtonRenderer.color = Palette.EnabledColor;
                    movePropButton.Timer = 0f;
                    movePropButton.isEffectActive = false;
                }
            }

            if (PropHuntPlugin.infectionMode)
            {
                DestroyableSingleton<RoleManager>.Instance.SetRole(__instance, RoleTypes.Impostor);
                __instance.Data.Role.TeamType = RoleTeamTypes.Impostor;
                __instance.Visible = true;
                __instance.cosmetics.SetPetVisible(true);

                foreach (DeadBody body in Object.FindObjectsOfType<DeadBody>())
                {
                    if (body.ParentId == __instance.PlayerId)
                    {
                        body.gameObject.SetActive(false);
                        break;
                    }
                }

                if (__instance.AmOwner)
                {
                    DestroyableSingleton<HudManager>.Instance.ShadowQuad.material.color = new Color(0f, 0f, 0f, 1f);
                }
            }
        }

        private static bool IsInfectionVictim(PlayerControl player)
        {
            return PropHuntPlugin.isPropHunt && PropHuntPlugin.infectionMode
                && player != null && player.Data != null && !player.Data.Role.IsImpostor;
        }

        [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRoleOnDeath))]
        [HarmonyPrefix]
        public static bool AssignRoleOnDeathPatch(PlayerControl player, bool specialRolesAllowed)
        {
            return !IsInfectionVictim(player);
        }

        [HarmonyPatch(typeof(HideAndSeekManager), nameof(HideAndSeekManager.OnPlayerDeath))]
        [HarmonyPrefix]
        public static bool HideAndSeekOnPlayerDeathPatch(PlayerControl player, bool assignGhostRole)
        {
            return !IsInfectionVictim(player);
        }

        [HarmonyPatch(typeof(SpriteAnim), nameof(SpriteAnim.Play))]
        [HarmonyPrefix]
        public static void SpriteAnimPlayPatch(AnimationClip anim, ref float speed)
        {
            if (!PropHuntPlugin.isPropHunt || anim == null || !anim.name.StartsWith("HnSSeekerSpawn")) return;
            speed = anim.length / Mathf.Max(1f, PropHuntPlugin.seekerWaitTime);
            Logger<PropHuntPlugin>.Info("Intro seek anim " + anim.name + ": " + anim.length.ToString("0.00") + "s -> speed " + speed.ToString("0.00"));
        }

        [HarmonyPatch(typeof(LogicGameFlowHnS), nameof(LogicGameFlowHnS.SeekerAdminMapEnabled))]
        [HarmonyPostfix]
        static void SeekerAdminMapEnabledPatch(LogicGameFlowHnS __instance, PlayerControl player, ref bool __result)
        {
            if (PropHuntPlugin.isPropHunt && !__instance.hideAndSeekManager.LogicOptionsHnS.GetSeekerFinalMap())
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
        [HarmonyPostfix]
        public static void KillButtonHighlightPatch(ActionButton __instance)
        {
            if (PropHuntPlugin.isPropHunt)
            {
                __instance.SetEnabled();
            }
        }

        [HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.IsValidTarget))]
        [HarmonyPrefix]
        public static bool ValidKillTargetPatch(ImpostorRole __instance, ref bool __result, NetworkedPlayerInfo target)
        {
            if (PropHuntPlugin.isPropHunt)
            {
                __result = !(target == null) && !target.Disconnected && !target.IsDead && target.PlayerId != __instance.Player.PlayerId && !(target.Role == null) && !(target.Object == null) && !target.Object.inVent && !target.Object.inMovingPlat && target.Role.CanBeKilled;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
        [HarmonyPrefix]
        public static void KillButtonClickPatch(KillButton __instance)
        {
            if (PropHuntPlugin.isPropHunt && __instance.currentTarget == null && !__instance.isCoolingDown && !PlayerControl.LocalPlayer.Data.IsDead && !PlayerControl.LocalPlayer.inVent)
            {
                RPCHandler.RPCFailedKill(PlayerControl.LocalPlayer);
                PlayerControl.LocalPlayer.SetKillTimer(3f);
            }
        }

        [HarmonyPatch(typeof(KillButton), nameof(KillButton.CheckClick))]
        [HarmonyPrefix]
        static bool KillButtonCheckClick(PlayerControl target)
        {
            return !PropHuntPlugin.isPropHunt;
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
        [HarmonyPostfix]
        public static void MinPlayerPatch(GameStartManager __instance)
        {
            __instance.MinPlayers = PropHuntPlugin.isPropHunt ? 2 : 4;
        }

        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
        [HarmonyPostfix]
        public static void PreventZeroImpPatch(ref int __result)
        {
            if (__result <= 0)
            {
                __result = 1;
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
        [HarmonyPostfix]
        public static void IntroCuscenePatch()
        {
            ShadowCollab shadowCollab = Object.FindObjectOfType<ShadowCollab>();
            if (PropHuntPlugin.isPropHunt)
            {
                foreach (NetworkedPlayerInfo player in GameData.Instance.AllPlayers)
                {
                    player.Object.transform.FindChild("BodyForms").localPosition = new Vector3(0, 0, -5);
                    player.Object.transform.FindChild("Cosmetics").localPosition = new Vector3(0, 0, -5);
                }

                if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
                {
                    shadowCollab.ShadowQuad.material.color = new Color(0, 0, 0, 1);
                    shadowCollab.ShadowQuad.gameObject.SetActive(true);
                }
                else
                {
                    shadowCollab.ShadowQuad.gameObject.SetActive(false);
                }

                DestroyableSingleton<HudManager>.Instance.Chat.SetVisible(true);
                DestroyableSingleton<HudManager>.Instance.MatchInfoButton.gameObject.SetActive(false);
            }
            else
            {
                foreach (NetworkedPlayerInfo player in GameData.Instance.AllPlayers)
                {
                    player.Object.transform.FindChild("BodyForms").localPosition = new Vector3(0, 0, 0);
                    player.Object.transform.FindChild("Cosmetics").localPosition = new Vector3(0, 0, 0);
                }

                shadowCollab.ShadowQuad.gameObject.SetActive(true);
                shadowCollab.ShadowQuad.material.color = new Color(0.2745f, 0.2745f, 0.2745f, 1);
            }
        }

        private static bool TryGetAliveProp(PlayerControl player, out SpriteRenderer renderer)
        {
            return PropManager.playerToProp.TryGetValue(player, out renderer) && renderer != null;
        }
    }
}
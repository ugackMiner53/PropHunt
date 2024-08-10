// Patches for PropHuntPlugin
// Copyright (C) 2024 ugackMiner

using HarmonyLib;
using Reactor;
using UnityEngine;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;

namespace PropHunt
{
    public class Patches
    {

        // Main input loop for custom keys
        [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
        [HarmonyPrefix]
        public static bool PlayerInputControlPatch(KeyboardJoystick __instance)
        {
            PlayerControl player = PlayerControl.LocalPlayer;

            if (!PropHuntPlugin.isPropHunt || player.Data.Role.IsImpostor || KeyboardJoystick.player == null) return true;

            // Change Prop
            if (Input.GetKeyDown(KeyCode.R)) // KeyboardJoystick.player.GetButtonDown(49) for Use Ability keybind
            {
                Logger<PropHuntPlugin>.Info("Key pressed");
                GameObject closestConsole = Utility.FindClosestConsole(player.gameObject, 3);
                if (closestConsole != null)
                {
                    for (int i = 0; i < ShipStatus.Instance.AllConsoles.Length; i++)
                    {
                        if (ShipStatus.Instance.AllConsoles[i] == closestConsole.GetComponent<Console>())
                        {
                            Logger<PropHuntPlugin>.Info("Task of index " + i + " being sent out");
                            RPCHandler.RPCPropSync(PlayerControl.LocalPlayer, i + "");
                            break;
                        }
                    }
                }
            }

            // Move Prop
            if (PropManager.playerToProp.ContainsKey(player)) 
            {
                if (Input.GetKey(KeyCode.LeftShift)) { // KeyboardJoystick.player.GetButton(7) for Report Button
                    // Disable default movement
                    __instance.del = Vector2.zero;
                    Vector2 inputDirection = new Vector2();

                    if (KeyboardJoystick.player.GetButton(40)) {
                        inputDirection.x += 1f;
                    }
                    if (KeyboardJoystick.player.GetButton(39)){
                        inputDirection.x -= 1f;
                    }
                    if (KeyboardJoystick.player.GetButton(44)) {
                        inputDirection.y += 1f;
                    }
                    if (KeyboardJoystick.player.GetButton(42)) {
                        inputDirection.y -= 1f;
                    }

                    Transform prop = PropManager.playerToProp[player].transform;
                    Vector3 newPosition = new Vector3(prop.localPosition.x + inputDirection.x * PropHuntPlugin.propMoveSpeed * Time.deltaTime, prop.localPosition.y + inputDirection.y * PropHuntPlugin.propMoveSpeed * Time.deltaTime, -3);

                    // Limit position to within kill distance
                    if (Vector2.Distance(Vector2.zero, newPosition) < PropHuntPlugin.maxPropDistance) {
                        prop.localPosition = newPosition;
                    }

                    return false;

                } else if (Input.GetKeyUp(KeyCode.LeftShift)) { // KeyboardJoystick.player.GetButtonUp(7) for Report button
                    Transform prop = PropManager.playerToProp[player].transform;
                    RPCHandler.RPCPropPos(player, prop.localPosition);
                }
            }

            return true;
        }


        // Runs when the player is created
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
        [HarmonyPostfix]
        public static void PlayerControlStartPatch(PlayerControl __instance)
        {
            // Move prop sprite down
            GameObject propObj = new GameObject("Prop") {
                layer = 11
            };
            SpriteRenderer propRenderer = propObj.AddComponent<SpriteRenderer>();
            propObj.transform.SetParent(__instance.transform);
            propObj.transform.localScale = Vector2.one;
            propObj.transform.localPosition = new Vector3(0, 0, -3);
            PropManager.playerToProp.Add(__instance, propRenderer);
        }


        // Reset PropManager prop map when leaving game
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
        [HarmonyPostfix]
        public static void OnExitGame() 
        {
            PropManager.playerToProp.Clear();
        }


        // Runs periodically, resets animation data for players
        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ResetAnimState))]
        [HarmonyPostfix]
        public static void PlayerPhysicsResetAnimationPatch(PlayerPhysics __instance)
        {
            if (!AmongUsClient.Instance.IsGameStarted || !PropHuntPlugin.isPropHunt || __instance.myPlayer == null)
                return;

            if (__instance.myPlayer.Visible && !__instance.myPlayer.Data.Role.IsImpostor && !__instance.myPlayer.Data.IsDead && PropManager.playerToProp.ContainsKey(__instance.myPlayer) && PropManager.playerToProp[__instance.myPlayer].sprite != null)
            {
                __instance.myPlayer.Visible = false;
            }
        }


        // Remove Prop on death & Make impostor if infection
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
        [HarmonyPostfix]
        public static void OnPlayerDiePatch(PlayerControl __instance) 
        {
            if (!PropHuntPlugin.isPropHunt || __instance.Data.Role.IsImpostor) return;

            SpriteRenderer prop = PropManager.playerToProp[__instance];
            if (prop != null) 
            {
                Logger<PropHuntPlugin>.Info("Removing Prop Lol!");
                prop.gameObject.Destroy();
                PropManager.playerToProp.Remove(__instance);
            }
        }


        // Prevent seeker admin map from appearing when there's 1 player left if the final map setting is off
        [HarmonyPatch(typeof(LogicGameFlowHnS), nameof(LogicGameFlowHnS.SeekerAdminMapEnabled))]
        [HarmonyPostfix]
        static void SeekerAdminMapEnabledPatch(LogicGameFlowHnS __instance, PlayerControl player, ref bool __result) 
        {
            if (PropHuntPlugin.isPropHunt && !__instance.hideAndSeekManager.LogicOptionsHnS.GetSeekerFinalMap()) {
                __result = false;
            }
        }


        // Make it so that the kill button doesn't light up when near a player
        [HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
        [HarmonyPostfix]
        public static void KillButtonHighlightPatch(ActionButton __instance)
        {
            if (PropHuntPlugin.isPropHunt) {
                __instance.SetEnabled();
            }
        }


        // Make impostor able to kill invisible players
        [HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.IsValidTarget))]
        [HarmonyPrefix]
        public static bool ValidKillTargetPatch(ImpostorRole __instance, ref bool __result, NetworkedPlayerInfo target) 
        {
            if (PropHuntPlugin.isPropHunt) {
                __result = !(target == null) && !target.Disconnected && !target.IsDead && target.PlayerId != __instance.Player.PlayerId && !(target.Role == null) && !(target.Object == null) && !target.Object.inVent && !target.Object.inMovingPlat && target.Role.CanBeKilled;
                return false;
            }
            return true;
        }


        // Penalize the impostor if there is no prop killed
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

        
        // Prevent clicking on players to kill them 
        [HarmonyPatch(typeof(KillButton), nameof(KillButton.CheckClick))]
        [HarmonyPrefix]
        static bool KillButtonCheckClick(PlayerControl target)
        {
            return !PropHuntPlugin.isPropHunt;
        }


        // Allow the game to start with < 2 players
        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
        [HarmonyPostfix]
        public static void MinPlayerPatch(GameStartManager __instance)
        {
            __instance.MinPlayers = PropHuntPlugin.isPropHunt ? 2 : 4;
        }


        // Prevent game from not having an impostor with < 2 players
        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
        [HarmonyPostfix]
        public static void PreventZeroImpPatch(ref int __result) 
        {
            if (__result <= 0) {
                __result = 1;
            }
        }


        // Set components on game start
        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
        [HarmonyPostfix]
        public static void IntroCuscenePatch()
        {
            ShadowCollab shadowCollab = Object.FindObjectOfType<ShadowCollab>();
            if (PropHuntPlugin.isPropHunt) {

                // Move player layers up
                foreach (NetworkedPlayerInfo player in GameData.Instance.AllPlayers) 
                {
                    player.Object.transform.FindChild("BodyForms").localPosition = new Vector3(0, 0, -5);
                    player.Object.transform.FindChild("Cosmetics").localPosition = new Vector3(0, 0, -5);
                }

                // Change visibility through walls for impostors & props
                if (PlayerControl.LocalPlayer.Data.Role.IsImpostor) {
                    shadowCollab.ShadowQuad.material.color = new Color(0, 0, 0, 1);
                    shadowCollab.ShadowQuad.gameObject.SetActive(true);
                } else {
                    shadowCollab.ShadowQuad.gameObject.SetActive(false);
                }

                // Enable the chat
                DestroyableSingleton<HudManager>.Instance.Chat.SetVisible(true);

            } else {

                // Reset player layers
                foreach (NetworkedPlayerInfo player in GameData.Instance.AllPlayers) 
                {
                    player.Object.transform.FindChild("BodyForms").localPosition = new Vector3(0, 0, 0);
                    player.Object.transform.FindChild("Cosmetics").localPosition = new Vector3(0, 0, 0);
                }

                // Reset shadow patches
                shadowCollab.ShadowQuad.gameObject.SetActive(true);
                shadowCollab.ShadowQuad.material.color = new Color(0.2745f, 0.2745f, 0.2745f, 1);
                
            }
        }
    
    }
}

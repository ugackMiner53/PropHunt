// Patches for PropHuntPlugin
// Copyright (C) 2022  ugackMiner
using HarmonyLib;
using Reactor;
using UnityEngine;
using AmongUs.Data;
using Reactor.Utilities;
using AmongUs.GameOptions;
using Reactor.Networking.Rpc;

namespace PropHunt
{
    public class Patches
    {
        // Main input loop for custom keys
        [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
        [HarmonyPostfix]
        public static void PlayerInputControlPatch(KeyboardJoystick __instance)
        {
            PlayerControl player = PlayerControl.LocalPlayer;
            if (Input.GetKeyDown(KeyCode.R) && !player.Data.Role.IsImpostor)
            {
                Logger<PropHuntPlugin>.Info("Key pressed");
                GameObject closestConsole = Utility.FindClosestConsole(player.gameObject, 3);
                if (closestConsole != null)
                {
                    SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
                    spriteRenderer.transform.localScale = closestConsole.transform.lossyScale;
                    spriteRenderer.sprite = closestConsole.GetComponent<SpriteRenderer>().sprite;

                    for (int i = 0; i < ShipStatus.Instance.AllConsoles.Length; i++)
                    {
                        if (ShipStatus.Instance.AllConsoles[i] == closestConsole.GetComponent<Console>())
                        {
                            Logger<PropHuntPlugin>.Info("Task of index " + i + " being sent out");
                            RPCHandler.RPCPropSync(PlayerControl.LocalPlayer, i + "");
                        }
                    }
                }
            }
        }

        // Runs when the player is created
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
        [HarmonyPostfix]
        public static void PlayerControlStartPatch(PlayerControl __instance)
        {
            __instance.gameObject.AddComponent<SpriteRenderer>();
        }


        // Runs periodically, resets animation data for players
        [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation")]
        [HarmonyPostfix]
        public static void PlayerPhysicsAnimationPatch(PlayerPhysics __instance)
        {
            if (!AmongUsClient.Instance.IsGameStarted || !PropHuntPlugin.isPropHunt)
                return;
            
            if (__instance.myPlayer.Visible && __instance.GetComponent<SpriteRenderer>().sprite != null && !__instance.myPlayer.Data.Role.IsImpostor)
            {
                __instance.myPlayer.Visible = false;
            }

            if (!__instance.myPlayer.Visible && __instance.myPlayer.Data.IsDead)
            {
                __instance.myPlayer.Visible = true;
                GameObject.Destroy(__instance.GetComponent<SpriteRenderer>());
            }
        }

        // Make prop impostor on death
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
        [HarmonyPostfix]
        public static void MakePropImpostorPatch(PlayerControl __instance)
        {
            if (PropHuntPlugin.isPropHunt && !__instance.Data.Role.IsImpostor && PropHuntPlugin.infection)
            {
                __instance.Revive();
                __instance.Data.Role.TeamType = RoleTeamTypes.Impostor;
                DestroyableSingleton<RoleManager>.Instance.SetRole(__instance, AmongUs.GameOptions.RoleTypes.Impostor);
                __instance.transform.localScale = new Vector3(0.7f, 0.7f, 1);
                __instance.Visible = true;
                // foreach (SpriteRenderer rend in __instance.GetComponentsInChildren<SpriteRenderer>())
                // {
                //     rend.sortingOrder += 5;
                // }
            }
        }

        // Make it so that the kill button doesn't light up when near a player
        [HarmonyPatch(typeof(VentButton), nameof(VentButton.SetTarget))]
        [HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
        [HarmonyPostfix]
        public static void KillButtonHighlightPatch(ActionButton __instance)
        {
            __instance.SetEnabled();
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
                Logger<PropHuntPlugin>.Warning("Not RPC failed kill");
                PlayerControl.LocalPlayer.SetKillTimer(3f);
                Coroutines.Start(Utility.KillConsoleAnimation());
                GameObject closestProp = Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance));
                if (closestProp != null)
                {
                    GameObject.Destroy(closestProp.gameObject);
                }
            }
        }

        // Make the game start with AT LEAST one impostor (happens if there are >4 players)
        [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.TryGetInt))]
        [HarmonyPrefix]
        public static bool ForceNotZeroImps(GameOptionsData __instance, Int32OptionNames optionName, out int value)
        {
            // This is a bad way of doing it because it gets called too often. 
            // TODO: Find another override!
            value = 0;
            if (optionName == Int32OptionNames.NumImpostors) {
                Logger<PropHuntPlugin>.Info("Overriding number of impostors!");
                    value = 1;
                // if (PropHuntPlugin.isPropHunt && __instance.NumImpostors <= 0) {
                    return false;
                // }
            }
            return true;
        }

        // Disable the validation check for maximum impostors
        [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.Validate))]
        [HarmonyPrefix]
        public static bool DisableMinImpValidation(GameOptionsData __instance, ref bool __result) 
        {
            if (PropHuntPlugin.isPropHunt) {
                __result = false;
                return false;
            }
            return true;
        }



        // Change the minimum amount of players to start a game
        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
        [HarmonyPostfix]
        public static void MinPlayerPatch(GameStartManager __instance)
        {
            __instance.MinPlayers = 2;
        }

        // Disable a lot of stuff (not needed anymore bcause of hidenseek)
        // [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
        // [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
        // [HarmonyPatch(typeof(Vent), nameof(Vent.Use))]
        // [HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
        // [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowCountOverlay))]
        // [HarmonyPrefix]
        // public static bool DisableFunctions()
        // {
        //     return false;
        // }

        [HarmonyPatch(typeof(ShadowCollab), nameof(ShadowCollab.OnEnable))]
        [HarmonyPrefix]
        public static bool DisableShadows(ShadowCollab __instance)
        {
            __instance.ShadowQuad.gameObject.SetActive(false);
            return false;
        }

        // Reset variables on game start
        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
        [HarmonyPostfix]
        public static void IntroCuscenePatch()
        {

            // if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            // {
            //     foreach (SpriteRenderer rend in PlayerControl.LocalPlayer.GetComponentsInChildren<SpriteRenderer>())
            //     {
            //         rend.sortingOrder += 5;
            //     }
            // }
            DestroyableSingleton<HudManager>.Instance.Chat.SetVisible(true);
        }
    }
}

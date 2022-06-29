// Patches for PropHuntPlugin
// Copyright (C) 2022  ugackMiner
using HarmonyLib;
using Reactor;
using UnityEngine;

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
                GameObject closestConsole = PropHuntPlugin.Utility.FindClosestConsole(player.gameObject, 3);
                if (closestConsole != null)
                {
                    player.transform.localScale = closestConsole.transform.lossyScale;
                    player.GetComponent<SpriteRenderer>().sprite = closestConsole.GetComponent<SpriteRenderer>().sprite;
                    for (int i = 0; i < ShipStatus.Instance.AllConsoles.Length; i++)
                    {
                        if (ShipStatus.Instance.AllConsoles[i] == closestConsole.GetComponent<Console>())
                        {
                            Logger<PropHuntPlugin>.Info("Task of index " + i + " being sent out");
                            PropHuntPlugin.RPCHandler.RPCPropSync(PlayerControl.LocalPlayer, i + "");
                        }
                    }
                }
            }
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                player.Collider.enabled = false;
            }
            else if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                player.Collider.enabled = true;
            }
        }

        // Runs when the player is created
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
        [HarmonyPostfix]
        public static void PlayerControlStartPatch(PlayerControl __instance)
        {
            __instance.gameObject.AddComponent<SpriteRenderer>();
            __instance.GetComponent<CircleCollider2D>().radius = 0.00001f;
            if (AmongUsClient.Instance.GameMode != GameModes.FreePlay)
            {
                GameObject.FindObjectOfType<PingTracker>().enabled = false;
            }
        }


        // Runs periodically, resets animation data for players
        [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation")]
        [HarmonyPostfix]
        public static void PlayerPhysicsAnimationPatch(PlayerPhysics __instance)
        {
            if (!AmongUsClient.Instance.IsGameStarted)
                return;
            if (__instance.GetComponent<SpriteRenderer>().sprite != null && !__instance.myPlayer.Data.Role.IsImpostor)
            {
                __instance.myPlayer.Visible = false;
            }
            if (__instance.myPlayer.Data.IsDead)
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
            if (!__instance.Data.Role.IsImpostor && PropHuntPlugin.infection)
            {
                foreach (GameData.TaskInfo task in __instance.Data.Tasks)
                {
                    task.Complete = true;
                }
                GameData.Instance.RecomputeTaskCounts();
                __instance.Revive();
                __instance.Data.Role.TeamType = RoleTeamTypes.Impostor;
                DestroyableSingleton<RoleManager>.Instance.SetRole(__instance, RoleTypes.Impostor);
                __instance.transform.localScale = new Vector3(0.7f, 0.7f, 1);
                __instance.Visible = true;
                foreach (SpriteRenderer rend in __instance.GetComponentsInChildren<SpriteRenderer>())
                {
                    rend.sortingOrder += 5;
                }
            }
        }

        // Make it so that seekers only win if they got ALL the props
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CheckEndCriteria))]
        [HarmonyPrefix]
        public static bool CheckEndPatch(ShipStatus __instance)
        {
            if (!GameData.Instance || TutorialManager.InstanceExists)
            {
                return false;
            }
            int crew = 0;
            int aliveImpostors = 0;
            int impostors = 0;
            for (int i = 0; i < GameData.Instance.PlayerCount; i++)
            {
                GameData.PlayerInfo playerInfo = GameData.Instance.AllPlayers[i];
                if (!playerInfo.Disconnected)
                {
                    if (playerInfo.Role.IsImpostor)
                    {
                        impostors++;
                    }
                    if (!playerInfo.IsDead)
                    {
                        if (playerInfo.Role.IsImpostor)
                        {
                            aliveImpostors++;
                        }
                        else
                        {
                            crew++;
                        }
                    }
                }
            }
            if (crew <= 0)
            {
                if (DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverImpostorKills, (UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Object>)System.Array.Empty<object>()));
                    ShipStatus.ReviveEveryone();
                    return false;
                }
                if (PlayerControl.GameOptions.gameType == GameType.Normal)
                {
                    GameOverReason endReason;
                    switch (TempData.LastDeathReason)
                    {
                        case DeathReason.Exile:
                            endReason = GameOverReason.ImpostorByVote;
                            break;
                        case DeathReason.Kill:
                            endReason = GameOverReason.ImpostorByKill;
                            break;
                        default:
                            endReason = GameOverReason.ImpostorByVote;
                            break;
                    }
                    ShipStatus.RpcEndGame(endReason, !SaveManager.BoughtNoAds);
                    return false;
                }
            }
            else if (!DestroyableSingleton<TutorialManager>.InstanceExists)
            {
                if (PlayerControl.GameOptions.gameType == GameType.Normal && GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
                {
                    __instance.enabled = false;
                    ShipStatus.RpcEndGame(GameOverReason.HumansByTask, !SaveManager.BoughtNoAds);
                    return false;
                }
            }
            else
            {
                bool allComplete = true;
                foreach (PlayerTask t in PlayerControl.LocalPlayer.myTasks)
                {
                    if (!t.IsComplete)
                    {
                        allComplete = false;
                    }
                }
                if (allComplete)
                {
                    DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverTaskWin, (UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Object>)System.Array.Empty<object>()));
                    __instance.Begin();
                }

            }
            if (aliveImpostors <= 0)
            {
                ShipStatus.RpcEndGame(GameOverReason.HumansByVote, !SaveManager.BoughtNoAds);
                return false;
            }
            return false;
        }

        // Make it so that the kill button doesn't light up when near a player
        [HarmonyPatch(typeof(VentButton), nameof(VentButton.SetTarget))]
        [HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
        [HarmonyPostfix]
        public static void KillButtonHighlightPatch(ActionButton __instance)
        {
            __instance.SetEnabled();
        }


        // Penalize the impostor if there is no prop killed
        [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
        [HarmonyPrefix]
        public static void KillButtonClickPatch(KillButton __instance)
        {
            if (__instance.currentTarget == null && !__instance.isCoolingDown && !PlayerControl.LocalPlayer.Data.IsDead && !PlayerControl.LocalPlayer.inVent)
            {
                PropHuntPlugin.missedKills++;
                if (AmongUsClient.Instance.GameMode != GameModes.FreePlay)
                {
                    TMPro.TextMeshPro pingText = GameObject.FindObjectOfType<PingTracker>().text;
                    pingText.text = string.Format("Remaining Attempts: {0}", PropHuntPlugin.maxMissedKills - PropHuntPlugin.missedKills);
                    pingText.color = Color.red;
                }
                if (PropHuntPlugin.missedKills >= PropHuntPlugin.maxMissedKills)
                {
                    PlayerControl.LocalPlayer.CmdCheckMurder(PlayerControl.LocalPlayer);
                    PropHuntPlugin.missedKills = 0;
                }
                Coroutines.Start(PropHuntPlugin.Utility.KillConsoleAnimation());
                GameObject closestProp = PropHuntPlugin.Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, GameOptionsData.KillDistances[Mathf.Clamp(PlayerControl.GameOptions.KillDistance, 0, 2)]);
                if (closestProp != null)
                {
                    GameObject.Destroy(closestProp.gameObject);
                }
            }
        }

        // Make the game start with AT LEAST one impostor (happens if there are >4 players)
        [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.GetAdjustedNumImpostors))]
        [HarmonyPrefix]
        public static bool ForceNotZeroImps(GameOptionsData __instance, ref int __result)
        {
            int numImpostors = PlayerControl.GameOptions.NumImpostors;
            int num = 3;
            if (GameData.Instance.PlayerCount < GameOptionsData.MaxImpostors.Length)
            {
                num = GameOptionsData.MaxImpostors[GameData.Instance.PlayerCount];
                if (num <= 0)
                {
                    num = 1;
                }
            }
            __result = Mathf.Clamp(numImpostors, 1, num);
            return false;
        }



        // Change the minimum amount of players to start a game
        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
        [HarmonyPostfix]
        public static void MinPlayerPatch(GameStartManager __instance)
        {
            __instance.MinPlayers = 2;
        }

        // Disable a lot of stuff
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
        [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
        [HarmonyPatch(typeof(Vent), nameof(Vent.Use))]
        [HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
        [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowCountOverlay))]
        [HarmonyPrefix]
        public static bool DisableFunctions()
        {
            return false;
        }

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
            PropHuntPlugin.missedKills = 0;
            if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                foreach (SpriteRenderer rend in PlayerControl.LocalPlayer.GetComponentsInChildren<SpriteRenderer>())
                {
                    rend.sortingOrder += 5;
                }
            }
            HudManager hud = DestroyableSingleton<HudManager>.Instance;
            hud.ImpostorVentButton.gameObject.SetActiveRecursively(false);
            hud.SabotageButton.gameObject.SetActiveRecursively(false);
            hud.ReportButton.gameObject.SetActiveRecursively(false);
            hud.Chat.SetVisible(true);
            Logger<PropHuntPlugin>.Info(PropHuntPlugin.hidingTime + " -- " + PropHuntPlugin.maxMissedKills);
        }

        // Change the role text
        [HarmonyPatch(typeof(IntroCutscene._ShowRole_d__24), nameof(IntroCutscene._ShowRole_d__24.MoveNext))]
        [HarmonyPostfix]
        public static void IntroCutsceneRolePatch(IntroCutscene._ShowRole_d__24 __instance)
        {
            // IEnumerator hooking (help from @Daemon#6489 in the reactor discord)
            if (__instance.__1__state == 1)
            {
                if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
                {
                    __instance.__4__this.RoleText.text = "Seeker";
                    __instance.__4__this.RoleBlurbText.text = "Find and kill the props\nYour game will be unfrozen after " + PropHuntPlugin.hidingTime + " seconds";
                }
                else
                {
                    __instance.__4__this.RoleText.text = "Prop";
                    __instance.__4__this.RoleBlurbText.text = "Turn into props to hide from the seekers";
                }
            }
        }

        // Extend the intro cutscene for impostors
        [HarmonyPatch(typeof(IntroCutscene._CoBegin_d__19), nameof(IntroCutscene._CoBegin_d__19.MoveNext))]
        [HarmonyPrefix]
        public static bool IntroCutsceneCoBeginPatch(IntroCutscene._CoBegin_d__19 __instance)
        {
            if (__instance.__1__state != 2 || !PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                return true;
            }
            Coroutines.Start(PropHuntPlugin.Utility.IntroCutsceneHidePatch(__instance.__4__this));
            return false;
        }


    }
}

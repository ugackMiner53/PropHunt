// Patches for PropHunt
// Copyright (C) 2023  ugackMiner
using HarmonyLib;
using Reactor.Utilities.Extensions;
using Hazel;
using UnityEngine;
using AmongUs.Data;
using Reactor.Utilities;
using AmongUs.GameOptions;
using TMPro;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;

namespace PropHunt
{
    public class Patches
    {
        public static bool Sync = false;
        public static bool IsCommand = false;
         public static string NameStates;
         public static string NameSync = "";

        // add mod stamp is required from innersloth

        [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
        [HarmonyPostfix]
         public static void ShowModStamp(ModManager __instance)
         {
            __instance.ShowModStamp();
         }
         // Logo + Changing Ambience for MainMenu of AU
         [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    class TitleLogoPatch
    {
        public static GameObject amongUsLogo;
        public static GameObject Ambience;

        public static SpriteRenderer PropLogo {get; private set;}

        private static void Postfix()
        {
            amongUsLogo = GameObject.Find("LOGO-AU");

            var proplogo = new GameObject("LOGO-PROP");
            proplogo.transform.SetParent(GameObject.Find("RightPanel").transform, false);
            proplogo.transform.localPosition = new Vector3(-0.4f, 1f, 5f);

            PropLogo = proplogo.AddComponent<SpriteRenderer>();
            PropLogo.sprite = LoadSprite("PropHunt-Reactivited.PropHunt.RES.LP.png", 300f);

            if ((Ambience = GameObject.Find("Ambience")) != null)
            {
                Ambience.SetActive(false);
                var CustomBG = new GameObject("CustomAM-Prop");
                CustomBG.transform.position = new Vector3(2.095f, -0.25f, 520f);
                var bgRenderer = CustomBG.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = LoadSprite("PropHunt-Reactivited.PropHunt.RES.AM.png", 245f);
            }
        }
    }
        // Main input loop for custom keys
        [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
        [HarmonyPostfix]
        public static void PlayerInputControlPatch(KeyboardJoystick __instance)
        {

            var player = PlayerControl.LocalPlayer;
           
            if (Input.GetKeyDown(KeyCode.R) && !player.Data.Role.IsImpostor)
            {
                Logger<PropHunt>.Info("Key pressed");
                GameObject closestConsole = PropHunt.Utility.FindClosestConsole(player.gameObject, 3);
                if (closestConsole != null)
                {
                    player.Visible = false;
                    player.transform.localScale = closestConsole.transform.lossyScale;
                    player.GetComponent<SpriteRenderer>().sprite = closestConsole.GetComponent<SpriteRenderer>().sprite;
                    int t = 0;
                     foreach(var task in ShipStatus.Instance.AllConsoles)
                    {
                        t++;
                        if(task == closestConsole.GetComponent<Console>())
                        {
                            PluginSingleton<PropHunt>.Instance.Log.LogInfo("Task " + task.ToString() + " being sent out");
                            var writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, 
                            (byte)PropHunt.RPC.PropSync,  SendOption.Reliable);
                            writer.Write(player.PlayerId);
                            writer.Write(t);
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                            PropHunt.RPCHandler.RPCPropSync(player, t);
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
           if (__instance == PlayerControl.LocalPlayer) Sync = false;
        }


        // Runs periodically, resets animation data for players
        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
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
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Revive))]
        [HarmonyPostfix]
        public static void MakePropImpostorPatch(PlayerControl __instance)
        {
            if (__instance.Data.IsDead) return;
            if (!__instance.Data.Role.IsImpostor && PropHunt.infection)
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
                GameObject.Destroy(__instance.GetComponent<SpriteRenderer>());
                foreach (SpriteRenderer rend in __instance.GetComponentsInChildren<SpriteRenderer>())
                {
                    rend.sortingOrder += 5;
                }
            
            }
        }
      
         // Patching this because is required to remove dead body when respawan
        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ToggleButtonGlyphs))]
        public static void Postfix(GameStartManager __instance, ref bool enabled)
        {
             __instance.MakePublicButtonGlyph?.SetActive(enabled);
             __instance.StartButtonGlyphContainer.SetActive(enabled);
        }
    
         // Make it so that seekers only win if they got ALL the props
        [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
        [HarmonyPrefix]
        public static bool CheckEndPatch(LogicGameFlowNormal __instance)
        {
            if (PropHunt.Instance.Debug.Value) return false;
            if (!GameData.Instance || TutorialManager.InstanceExists) return false;

            int crew = 0, impostors = 0, aliveImpostors = 0;
            
            foreach(var pi in GameData.Instance.AllPlayers)
            {
                if (pi.Disconnected) continue;
                if (pi.Role.IsImpostor) impostors++;
                if (!pi.IsDead)
                {
                    if (pi.Role.IsImpostor)
                    {
                        aliveImpostors++;
                    }
                    else
                    {
                        crew++;
                    }   
                }
            }
            if (crew <= 0)
            {
                if (DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverImpostorKills, System.Array.Empty<Il2CppSystem.Object>()));
                    foreach (var pc in PlayerControl.AllPlayerControls) pc.Revive(); // =ShipStatus.ReviveEveryone()
                    return false;
                }
                if (GameOptionsManager.Instance.currentGameOptions.GameMode == GameModes.Normal)
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
                    GameManager.Instance.RpcEndGame(endReason, false);
                    return false;
                }
            }
            else if (!DestroyableSingleton<TutorialManager>.InstanceExists)
            {
                if (GameOptionsManager.Instance.currentGameOptions.GameMode == GameModes.Normal && GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
                {
                    ShipStatus.Instance.enabled = false;
                    GameManager.Instance.RpcEndGame(GameOverReason.HumansByTask, false);
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
                    DestroyableSingleton<HudManager>.Instance.ShowPopUp(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameOverTaskWin, System.Array.Empty<Il2CppSystem.Object>()));
                    ShipStatus.Instance.Begin();
                }

            }
            if (aliveImpostors <= 0)
            {
                GameManager.Instance.RpcEndGame(GameOverReason.HumansByVote, false);
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
            if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
            __instance.SetEnabled();
        }
         // Disable buttons
        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
        [HarmonyPostfix]
        public static void DisableButtonsPatch(HudManager __instance)
        {
            if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) return;
            __instance.SabotageButton.gameObject.SetActive(false);
            __instance.ReportButton.SetActive(false);
            __instance.ImpostorVentButton.gameObject.SetActive(false);
        }


        // Penalize the impostor if there is no prop killed
        [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
        [HarmonyPrefix]
        public static void KillButtonClickPatch(KillButton __instance)
        {
            if (__instance.currentTarget == null && !__instance.isCoolingDown && !PlayerControl.LocalPlayer.Data.IsDead && !PlayerControl.LocalPlayer.inVent)
            {
                PropHunt.missedKills++;
                if (AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
                {
                   
                    NameStates = string.Format($"<color=#ff0000>{Language.GetMessage(StringOptions.RemainAttempt)}</color>", PropHunt.maxMissedKills - PropHunt.missedKills);
                    
                }
                if (PropHunt.missedKills >= PropHunt.maxMissedKills)
                {
                    PlayerControl.LocalPlayer.CmdCheckMurder(PlayerControl.LocalPlayer);
                    NameStates = "";
                    PropHunt.missedKills = 0;
                }
                Coroutines.Start(PropHunt.Utility.KillConsoleAnimation());
                GameObject closestProp = PropHunt.Utility.FindClosestConsole(PlayerControl.LocalPlayer.gameObject, GameOptionsData.KillDistances[Mathf.Clamp(GameOptionsManager.Instance.currentNormalGameOptions.KillDistance, 0, 2)]);
                if (closestProp != null)
                {
                    GameObject.Destroy(closestProp.gameObject);
                }
            }
        }

        // Make the game start with AT LEAST one impostor (happens if there are >4 players)
        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
        [HarmonyPrefix]
        public static bool ForceNotZeroImps(ref int __result)
        {
            int numImpostors = GameOptionsManager.Instance.currentNormalGameOptions.NumImpostors;
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
            __instance.MinPlayers = 4;
        }

        // Disable a lot of stuff in freeplay in MMOnline stuff are not disabled just a cool features
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
            PropHunt.missedKills = 0;
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
            NameStates = "";
            PluginSingleton<PropHunt>.Instance.Log.LogInfo(PropHunt.hidingTime + " -- " + PropHunt.maxMissedKills);
        }

        // Change the role text
        [HarmonyPatch(typeof(IntroCutscene._ShowRole_d__39), nameof(IntroCutscene._ShowRole_d__39.MoveNext))]
        [HarmonyPostfix]
        public static void IntroCutsceneRolePatch(IntroCutscene._ShowRole_d__39 __instance)
        {
            // IEnumerator hooking (help from @Daemon#6489 in the reactor discord)
            if (__instance.__1__state == 1)
            {
                if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
                {
                    __instance.__4__this.RoleText.text = Language.GetMessage(StringOptions.Seeker);
                    __instance.__4__this.RoleBlurbText.text = string.Format(Language.GetMessage(StringOptions.SeekerDescription), PropHunt.hidingTime);
                    __instance.__4__this.RoleBlurbText.color = Palette.Orange; // custom colors
                }
                else
                {
                    __instance.__4__this.RoleText.text = Language.GetMessage(StringOptions.Prop);
                    __instance.__4__this.RoleBlurbText.text = Language.GetMessage(StringOptions.PropDescription);
                    __instance.__4__this.RoleBlurbText.color = Palette.Brown; 
                }
            }
        }

        // Player dead check + send in chat code from PropHunt-Plus
        [HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.CheckMurder))]
        [HarmonyPostfix]
        public static void PlayerDeadPatch(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            var killer = __instance;
            if (killer == null || target == null) return;
            if (!AmongUsClient.Instance.AmHost) return;
            if (killer == target && target.Data.Role.IsImpostor)
            {
                PluginSingleton<PropHunt>.Instance.Log.LogInfo("Imp ded");
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Language.GetMessage(StringOptions.SeekerDead));
            }
            else
            {
                 PluginSingleton<PropHunt>.Instance.Log.LogInfo("Crew ded/infcted");
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Language.GetMessage(PropHunt.infection ? StringOptions.PropInfected: StringOptions.PropDead));
            }
        }


        // Commands from PropHunt-Plus
        [HarmonyPatch(typeof(ChatController),nameof(ChatController.AddChat))]
        [HarmonyPostfix]
        public static void ChatCommandsPatch(ChatController __instance, [HarmonyArgument(0)] PlayerControl sourcePlayer, [HarmonyArgument(1)] string chatText)
        {
            IsCommand = false;
            if (!chatText.StartsWith('/')) return;
            if (sourcePlayer != PlayerControl.LocalPlayer) return;
            string[] cmd = chatText.Split(" ");
            switch (cmd[0].ToLower())
            {
                case "/km":
                    sourcePlayer.RpcMurderPlayer(sourcePlayer);
                    break;
                case "/help":
                    __instance.AddChat(sourcePlayer, Language.GetMessage(StringOptions.CmdHelp));
                    break;
                // For testing
                case "/m1":
                    var player = PlayerControl.AllPlayerControls.ToArray().Where(pc => pc.PlayerId == Convert.ToInt32(cmd[1])).FirstOrDefault();
                    sourcePlayer.RpcMurderPlayer(player);
                    break;
                case "/exit":
                AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
                break;
                case "/pid":
                    string a = "";
                    foreach (var pc in PlayerControl.AllPlayerControls)
                    {
                        a += pc.Data.PlayerName + " " + pc.PlayerId + "\r\n";
                    }
                   PluginSingleton<PropHunt>.Instance.Log.LogMessage(a);
                    break;
                case "/cid":
                    string i = "";
                    foreach (var pc in PlayerControl.AllPlayerControls)
                    {
                        i += pc.Data.PlayerName + " " + pc.NetId + "\n";
                    }
                    PluginSingleton<PropHunt>.Instance.Log.LogMessage(i);
                    break;
                case "/kick":
                    AmongUsClient.Instance.KickPlayer(Convert.ToInt32(cmd[1]), Convert.ToBoolean(cmd[2]));
                    break;
                case "/role":
                    if (cmd[1] == "0")
                    {
                        DestroyableSingleton<RoleManager>.Instance.SetRole(PlayerControl.LocalPlayer, RoleTypes.Crewmate);
                    }
                    else
                    {
                        DestroyableSingleton<RoleManager>.Instance.SetRole(PlayerControl.LocalPlayer, RoleTypes.Impostor);
                    }
                    break;
            }
        }

         [HarmonyPatch(typeof(EmergencyMinigame),nameof(EmergencyMinigame.Update))]
        [HarmonyPostfix]
        public static void EmergencyButtonPatch(EmergencyMinigame __instance)
        {
            if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) return;
            __instance.StatusText.text = Language.GetMessage(StringOptions.MeetingDisabled);
            __instance.NumberText.text = "";
            __instance.OpenLid.gameObject.SetActive(false);
            __instance.ClosedLid.gameObject.SetActive(true);
            __instance.ButtonActive = false;
        }
        
        // Extend the intro cutscene for impostors
        [HarmonyPatch(typeof(IntroCutscene._CoBegin_d__33), nameof(IntroCutscene._CoBegin_d__33.MoveNext))]
        [HarmonyPrefix]
        public static bool IntroCutsceneCoBeginPatch(IntroCutscene._CoBegin_d__33 __instance)
        {
            if (__instance.__1__state != 2 || !PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                return true;
            }
            Coroutines.Start(PropHunt.Utility.IntroCutsceneHidePatch(__instance.__4__this));
            return false;
        }

        public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
        {
        Sprite sprite = null;
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(path);
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            ImageConversion.LoadImage(texture, ms.ToArray());
            sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(0.5f, 0.5f), pixelsPerUnit);
        }
        catch
        {
            PluginSingleton<PropHunt>.Instance.Log.LogError($"\"{path}\"Failed To Load");
        }
          return sprite;
        }
    

        [HarmonyPatch(typeof(ChatBubble),nameof(ChatBubble.SetText))]
        [HarmonyPostfix]
        public static void NameFix(ChatBubble __instance)
        {
            int line = __instance.NameText.text.Split("\n").Length;
            string el = "";
            if (line > 1)
            {
                for (int i = 0; i < line - 1; i++)
                {
                    el += "\n";
                }
                el += __instance.TextArea.text;
                __instance.TextArea.text = el;
            }
        }
    }
}

// Core Script of PropHunt
// Copyright (C) 2022  ugackMiner
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor;
using Reactor.Utilities;
using Reactor.Networking.Rpc;
using Hazel;
using Reactor.Networking.Attributes;
using UnityEngine;

namespace PropHunt;


[BepInPlugin("com.jeanau.prophunt-r", "Prop Hunt-Reactivited", VersionString)]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public partial class PropHunt : BasePlugin
{
    public const string VersionString = "2023.8.25";

    // Backend Variables
    public Harmony Harmony { get; } = new("com.jeanau.prophunt-r");

    public ConfigEntry<int> HidingTime { get; private set; }
    public ConfigEntry<int> MaxMissedKills { get; private set; }
    public ConfigEntry<bool> Infection { get; private set; }
    public ConfigEntry<bool> Debug    {get; private set;}

    // Gameplay Variables
    public static int hidingTime
    {
        get => Instance.HidingTime.Value;
        set 
        {
            Instance.HidingTime.Value = value;
            Instance.Config.Save();
        }
    }
    public static int maxMissedKills
    {
        get => Instance.MaxMissedKills.Value;
        set
        {
            Instance.MaxMissedKills.Value = value;
            Instance.Config.Save();
        }
    }
    public static bool infection
    {
        get => Instance.Infection.Value;
        set
        {
            Instance.Infection.Value = value;
            Instance.Config.Save();
        }
    }


     public static int missedKills = 0;

     public static PropHunt Instance;

    public override void Load()
    {
        HidingTime = Config.Bind("Prop Hunt", "Hiding Time", 30);
        MaxMissedKills = Config.Bind("Prop Hunt", "Max Misses", 3);
        Infection = Config.Bind("Prop Hunt", "Infection", true);
        Debug = Config.Bind("Prop Hunt", "DebugManager", false);

        Instance = this;

        Harmony.PatchAll(typeof(Patches));
        Harmony.PatchAll(typeof(CustomRoleSettings));
        Harmony.PatchAll(typeof(Language));
        Harmony.PatchAll(typeof(RPCPatch));
        Harmony.PatchAll(typeof(VersionShower));

        PluginSingleton<PropHunt>.Instance.Log.LogMessage($"Success fully Loaded PropHunt-R v{VersionString}");
    }

    public enum RPC
    {
        PropSync,
        SettingSync
    }

    public static class RPCHandler
    {
        // static MethodRpc rpc = new MethodRpc(PropHunt.Instance, Type.GetMethod("RPCPropSync"), RPC.PropSync, Hazel.SendOption.Reliable, RpcLocalHandling.None, true);
        [MethodRpc((uint)RPC.PropSync)]
        public static void RPCPropSync(PlayerControl player, int propIndex)
        {
            GameObject prop = ShipStatus.Instance.AllConsoles[propIndex].gameObject;
            Logger<PropHunt>.Info($"{player.Data.PlayerName} changed their sprite to: {prop.name}");
            player.GetComponent<SpriteRenderer>().sprite = prop.GetComponent<SpriteRenderer>().sprite;
            player.transform.localScale = prop.transform.lossyScale;
            player.Visible = false;
        }

        [MethodRpc((uint)RPC.SettingSync)]
        public static void RPCSettingSync(PlayerControl player, int _hidingTime, int _missedKills, bool _infection)
        {
            hidingTime = _hidingTime;
            maxMissedKills = _missedKills;
            infection = _infection;
            Logger<PropHunt>.Info("H: " + PropHunt.hidingTime + ", M: " + PropHunt.maxMissedKills + ", I: " + PropHunt.infection);
            if (player == PlayerControl.LocalPlayer && (hidingTime != Instance.HidingTime.Value || maxMissedKills != Instance.MaxMissedKills.Value || infection != Instance.Infection.Value))
            {
                Instance.HidingTime.Value = hidingTime;
                Instance.MaxMissedKills.Value = maxMissedKills;
                Instance.Infection.Value = infection;
                Instance.Config.Save();
            }
        }
    }


    public static  class Utility
    {
        public static GameObject FindClosestConsole(GameObject origin, float radius)
        {
            Collider2D bestCollider = null;
            float bestDist = 9999;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(origin.transform.position, radius))
            {
                if (collider.GetComponent<Console>() != null)
                {
                    float dist = Vector2.Distance(origin.transform.position, collider.transform.position);
                    if (dist < bestDist)
                    {
                        bestCollider = collider;
                        bestDist = dist;
                    }
                }
            }
            return bestCollider.gameObject;
        } 

        public static System.Collections.IEnumerator KillConsoleAnimation()
        {
            if (Constants.ShouldPlaySfx())
            {
                SoundManager.Instance.PlaySound(ShipStatus.Instance.SabotageSound, false, 0.8f);
                DestroyableSingleton<HudManager>.Instance.FullScreen.color = new Color(1f, 0f, 0f, 0.372549027f);
                DestroyableSingleton<HudManager>.Instance.FullScreen.gameObject.SetActive(true);
                yield return new WaitForSeconds(0.5f);
               DestroyableSingleton<HudManager>.Instance.FullScreen.gameObject.SetActive(false);
            }
            yield break;
        }

        public static System.Collections.IEnumerator IntroCutsceneHidePatch(IntroCutscene __instance)
        {
            PlayerControl.LocalPlayer.moveable = false;
            yield return new WaitForSeconds(PropHunt.hidingTime);
            PlayerControl.LocalPlayer.moveable = true;
            Object.Destroy(__instance.gameObject);
        }
    }
}


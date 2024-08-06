using AmongUs.GameOptions;
using Reactor.Networking.Attributes;
using Reactor.Utilities;
using UnityEngine;

namespace PropHunt;

public enum RPC
{
    PropSync,
    PropPos,
    FailedKill,
    SettingSync
}

public static class RPCHandler
{
    // static MethodRpc rpc = new MethodRpc(PropHuntPlugin.Instance, Type.GetMethod("RPCPropSync"), RPC.PropSync, Hazel.SendOption.Reliable, RpcLocalHandling.None, true);
    [MethodRpc((uint)RPC.PropSync)]
    public static void RPCPropSync(PlayerControl player, string propIndex)
    {
        GameObject prop = ShipStatus.Instance.AllConsoles[int.Parse(propIndex)].gameObject;
        Logger<PropHuntPlugin>.Info($"{player.Data.PlayerName} changed their sprite to: {prop.name}");

        SpriteRenderer propRenderer = PropManager.playerToProp[player];
        propRenderer.transform.localScale = prop.transform.lossyScale * 1.429f;
        propRenderer.transform.localPosition = new Vector3(0, 0, 0);
        propRenderer.sprite = prop.GetComponent<SpriteRenderer>().sprite;
        player.Visible = false;
    }

    [MethodRpc((uint)RPC.PropPos)]
    public static void RPCPropPos(PlayerControl player, Vector2 position) 
    {
        PropManager.playerToProp[player].transform.localPosition = new Vector3(position.x, position.y, 0);
    }

    [MethodRpc((uint)RPC.FailedKill)]
    public static void RPCFailedKill(PlayerControl player) 
    {
        // Unsure if this is needed, decrease timer by missPenalty
        Logger<PropHuntPlugin>.Warning("RPC Failed Kill");
        GameManager.Instance.Cast<HideAndSeekManager>().LogicFlowHnS.AdjustEscapeTimer(PropHuntPlugin.missTimePenalty, true);
        Coroutines.Start(Utility.KillConsoleAnimation());
        GameObject closestProp = Utility.FindClosestConsole(player.gameObject, GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance));
        if (closestProp != null)
        {
            GameObject.Destroy(closestProp.gameObject);
        }
    }

    [MethodRpc((uint)RPC.SettingSync)]
    public static void RPCSettingSync(PlayerControl player, float _missTimePenalty, bool _infection)
    {
        PropHuntPlugin.missTimePenalty = _missTimePenalty;
        // Logger<PropHuntPlugin>.Info("H: " + PropHuntPlugin.hidingTime + ", M: " + PropHuntPlugin.maxMissedKills + ", I: " + PropHuntPlugin.infection);
        Logger<PropHuntPlugin>.Info($"MissTimePenalty {PropHuntPlugin.missTimePenalty}");
        if (player == PlayerControl.LocalPlayer && (PropHuntPlugin.missTimePenalty != PropHuntPlugin.Instance.MissTimePenalty.Value))
        {
            PropHuntPlugin.Instance.MissTimePenalty.Value = PropHuntPlugin.missTimePenalty;
            PropHuntPlugin.Instance.Config.Save();
        }
    }
}
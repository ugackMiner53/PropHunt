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
        player.GetComponent<SpriteRenderer>().sprite = prop.GetComponent<SpriteRenderer>().sprite;
        player.transform.localScale = prop.transform.lossyScale;
        player.Visible = false;
    }

    [MethodRpc((uint)RPC.PropPos)]
    public static void RPCPropPos(PlayerControl player, Vector2 position) 
    {
        
    }

    [MethodRpc((uint)RPC.FailedKill)]
    public static void FailedKill(PlayerControl player) 
    {
        // Unsure if this is needed, decrease timer by missPenalty
    }

    [MethodRpc((uint)RPC.SettingSync)]
    public static void RPCSettingSync(PlayerControl player, float _missTimePenalty, bool _infection)
    {
        PropHuntPlugin.missTimePenalty = _missTimePenalty;
        PropHuntPlugin.infection = _infection;
        // Logger<PropHuntPlugin>.Info("H: " + PropHuntPlugin.hidingTime + ", M: " + PropHuntPlugin.maxMissedKills + ", I: " + PropHuntPlugin.infection);
        Logger<PropHuntPlugin>.Info($"MissTimePenalty {PropHuntPlugin.missTimePenalty}, Infection {PropHuntPlugin.infection}");
        if (player == PlayerControl.LocalPlayer && (PropHuntPlugin.missTimePenalty != PropHuntPlugin.Instance.MissTimePenalty.Value || PropHuntPlugin.infection != PropHuntPlugin.Instance.Infection.Value))
        {
            PropHuntPlugin.Instance.MissTimePenalty.Value = PropHuntPlugin.missTimePenalty;
            PropHuntPlugin.Instance.Infection.Value = PropHuntPlugin.infection;
            PropHuntPlugin.Instance.Config.Save();
        }
    }
}
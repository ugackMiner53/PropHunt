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
    SettingSync,
    Revert
}

public static class RPCHandler
{
    [MethodRpc((uint)RPC.PropSync)]
    public static void RPCPropSync(PlayerControl player, string propIndex)
    {
        GameObject prop = ShipStatus.Instance.AllConsoles[int.Parse(propIndex)].gameObject;

        SpriteRenderer propRenderer = PropManager.playerToProp[player];
        propRenderer.transform.localScale = prop.transform.lossyScale * 1.429f;
        propRenderer.transform.localPosition = new Vector3(0, 0, -3);
        propRenderer.sprite = prop.GetComponent<SpriteRenderer>().sprite;
        player.Visible = false;
    }

    [MethodRpc((uint)RPC.PropPos)]
    public static void RPCPropPos(PlayerControl player, Vector2 position) 
    {
        PropManager.playerToProp[player].transform.localPosition = new Vector3(position.x, position.y, -3);
    }

    [MethodRpc((uint)RPC.Revert)]
    public static void RPCRevert(PlayerControl player)
    {
        if (PropManager.playerToProp.ContainsKey(player))
        {
            PropManager.playerToProp[player].sprite = null;
            player.Visible = true;
        }
    }

    [MethodRpc((uint)RPC.FailedKill)]
    public static void RPCFailedKill(PlayerControl player) 
    {
        GameManager.Instance.Cast<HideAndSeekManager>().LogicFlowHnS.AdjustEscapeTimer(PropHuntPlugin.missTimePenalty, true);
        Coroutines.Start(Utility.KillConsoleAnimation());
        GameObject closestProp = Utility.FindClosestConsole(player.gameObject, GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance) + 5);
        if (closestProp != null)
        {
            GameObject.Destroy(closestProp.gameObject);
        }
    }

    [MethodRpc((uint)RPC.SettingSync)]
    public static void RPCSettingSync(PlayerControl player, bool _isPropHunt, float _missTimePenalty, bool _infection)
    {
        PropHuntPlugin.isPropHunt = _isPropHunt;
        PropHuntPlugin.missTimePenalty = _missTimePenalty;
        
        if (player == PlayerControl.LocalPlayer && (PropHuntPlugin.isPropHunt != PropHuntPlugin.Instance.IsPropHunt.Value || PropHuntPlugin.missTimePenalty != PropHuntPlugin.Instance.MissTimePenalty.Value))
        {
            PropHuntPlugin.Instance.IsPropHunt.Value = PropHuntPlugin.isPropHunt;
            PropHuntPlugin.Instance.MissTimePenalty.Value = PropHuntPlugin.missTimePenalty;
            PropHuntPlugin.Instance.Config.Save();
        }

        if (GameStartManager.InstanceExists) {
            GameStartManager.Instance.MinPlayers = PropHuntPlugin.isPropHunt ? 2 : 4;
        }
    }
}
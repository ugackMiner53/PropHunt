using AmongUs.GameOptions;
using PropHunt.Settings;
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
    Revert,
    SeekerTransform
}

public static class RPCHandler
{
    [MethodRpc((uint)RPC.PropSync)]
    public static void RPCPropSync(PlayerControl player, string propIndex)
    {
        GameObject prop = ShipStatus.Instance.AllConsoles[int.Parse(propIndex)].gameObject;

        SpriteRenderer propRenderer = PropManager.playerToProp[player];
        propRenderer.transform.localScale = prop.transform.lossyScale * 1.429f;
        propRenderer.transform.localPosition = new Vector3(0, 0, 0);
        propRenderer.sprite = prop.GetComponent<SpriteRenderer>().sprite;
        player.Visible = false;
        // Hide the pet too - it is a separate object and would otherwise give
        // the disguised player away (especially to the seeker).
        player.cosmetics.SetPetVisible(false);
    }

    [MethodRpc((uint)RPC.PropPos)]
    public static void RPCPropPos(PlayerControl player, Vector2 position)
    {
        PropManager.playerToProp[player].transform.localPosition = new Vector3(position.x, position.y, 0);
    }

    [MethodRpc((uint)RPC.Revert)]
    public static void RPCRevert(PlayerControl player)
    {
        if (PropManager.playerToProp.ContainsKey(player))
        {
            PropManager.playerToProp[player].sprite = null;
            player.Visible = true;
            player.cosmetics.SetPetVisible(true);
        }
    }

    // Sent by a client when one of its players gets converted in infection mode,
    // so every other client replays the opening seeker transform (giant mouth).
    [MethodRpc((uint)RPC.SeekerTransform)]
    public static void RPCSeekerTransform(PlayerControl player)
    {
        Logger<PropHuntPlugin>.Info("RPCSeekerTransform received for player "
            + (player != null ? player.PlayerId.ToString() : "null"));
        Patches.ApplySeekerTransformVisual(player, false);
    }

    [MethodRpc((uint)RPC.FailedKill)]
    public static void RPCFailedKill(PlayerControl player)
    {
        GameManager.Instance.Cast<HideAndSeekManager>().LogicFlowHnS.AdjustEscapeTimer(PropHuntPlugin.missTimePenalty, true);
        Coroutines.Start(Utility.KillConsoleAnimation());
        Console closestProp = Utility.FindClosestConsole(player.gameObject, GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance) + 5);
        if (closestProp != null)
        {
            GameObject.Destroy(closestProp.gameObject);
        }
    }

    [MethodRpc((uint)RPC.SettingSync)]
    public static void RPCSettingSync(PlayerControl player, bool _isPropHunt, float _missTimePenalty, float _disguiseRange, float _disguiseCooldown, float _seekerWaitTime, bool _infectionMode)
    {
        bool propHuntChanged = _isPropHunt != PropHuntPlugin.isPropHunt;
        bool penaltyChanged = _missTimePenalty != PropHuntPlugin.missTimePenalty;
        bool rangeChanged = _disguiseRange != PropHuntPlugin.disguiseRange;
        bool cooldownChanged = _disguiseCooldown != PropHuntPlugin.disguiseCooldown;
        bool seekerWaitChanged = _seekerWaitTime != PropHuntPlugin.seekerWaitTime;
        bool infectionChanged = _infectionMode != PropHuntPlugin.infectionMode;

        PropHuntPlugin.isPropHunt = _isPropHunt;
        PropHuntPlugin.missTimePenalty = _missTimePenalty;
        PropHuntPlugin.disguiseRange = _disguiseRange;
        PropHuntPlugin.disguiseCooldown = _disguiseCooldown;
        PropHuntPlugin.seekerWaitTime = _seekerWaitTime;
        PropHuntPlugin.infectionMode = _infectionMode;

        // Keep the custom settings menu in sync on every client
        PropHuntOptions.UpdateFromPlugin();

        // Persist to config when the local player is the one who made the change
        if (player == PlayerControl.LocalPlayer &&
            (propHuntChanged || penaltyChanged || rangeChanged || cooldownChanged || seekerWaitChanged || infectionChanged))
        {
            PropHuntPlugin.Instance.IsPropHunt.Value = PropHuntPlugin.isPropHunt;
            PropHuntPlugin.Instance.MissTimePenalty.Value = PropHuntPlugin.missTimePenalty;
            PropHuntPlugin.Instance.DisguiseRange.Value = PropHuntPlugin.disguiseRange;
            PropHuntPlugin.Instance.DisguiseCooldown.Value = PropHuntPlugin.disguiseCooldown;
            PropHuntPlugin.Instance.SeekerWaitTime.Value = PropHuntPlugin.seekerWaitTime;
            PropHuntPlugin.Instance.InfectionMode.Value = PropHuntPlugin.infectionMode;
            PropHuntPlugin.Instance.Config.Save();
            PropHuntOptions.SaveOptions();
        }

        // Show change notification to everyone (host and non-host alike)
        // Only show if we're in a lobby (HudManager exists) and something actually changed
        if (propHuntChanged)
        {
            string value = _isPropHunt
                ? DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.SettingsOn)
                : DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.SettingsOff);
            PropHuntOptions.ShowSettingNotification("Prop Hunt", value);
        }

        if (penaltyChanged)
        {
            PropHuntOptions.ShowSettingNotification("Miss Penalty", _missTimePenalty.ToString("0.0#") + "s");
        }

        if (rangeChanged)
        {
            PropHuntOptions.ShowSettingNotification("Disguise Range", _disguiseRange.ToString("0.#"));
        }

        if (cooldownChanged)
        {
            PropHuntOptions.ShowSettingNotification("Disguise Cooldown", _disguiseCooldown.ToString("0") + "s");
        }

        if (seekerWaitChanged)
        {
            PropHuntOptions.ShowSettingNotification("Seeker Wait Time", _seekerWaitTime.ToString("0") + "s");
        }

        if (infectionChanged)
        {
            string value = _infectionMode
                ? DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.SettingsOn)
                : DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.SettingsOff);
            PropHuntOptions.ShowSettingNotification("Infection Mode", value);
        }

        // Adjust min player count based on game mode
        if (GameStartManager.InstanceExists)
        {
            GameStartManager.Instance.MinPlayers = PropHuntPlugin.isPropHunt ? 2 : 4;
        }
    }
}

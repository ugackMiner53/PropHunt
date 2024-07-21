using AmongUs.GameOptions;
using Hazel;
using Il2CppSystem.Linq;
using PropHunt;

public class PropHuntGameOptions : HideNSeekGameOptionsV08
{
    public PropHuntGameOptions(ILogger logger) : base(logger)
    {
        UnityEngine.Debug.Log("Prop Hunt game options init");
        this.GameMode = PropHuntPlugin.PropHuntGameMode;
    }

    #region April Fools Removal
    public override GameModes AprilFoolsOnMode {get {return PropHuntPlugin.PropHuntGameMode;}}
    public override GameModes AprilFoolsOffMode {get {return PropHuntPlugin.PropHuntGameMode;}}
    #endregion


    // public float incorrectTimeRemoval = 15f;



    public override void SetRecommendations(int numPlayers, bool isOnline, RulesPresets rulesPresets)
    {
        base.SetRecommendations(numPlayers, isOnline, rulesPresets);

        // Disable specific parts of hide & seek that don't fit prop hunt
        this.SeekerFinalMap = false;
        this.SeekerPings = false;
        this.CrewmateVentUses = 0;
        this.CrewmateTimeInVent = 0f;
        this.MaxPingTime = 0f;

    }

}
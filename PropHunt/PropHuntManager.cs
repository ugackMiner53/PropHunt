using Reactor.Utilities;

namespace PropHunt;

public class PropHuntManager : HideAndSeekManager 
{
    public override void StartGame()
    {
        Logger<PropHuntPlugin>.Info("PropHuntManager Initialized!");
        base.StartGame();
    }

}
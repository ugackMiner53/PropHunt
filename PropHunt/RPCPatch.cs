using HarmonyLib;
using Hazel;
using System.Data;
using System.Linq;
using static PropHunt.PropHunt;

namespace PropHunt
{
    public enum RPC
    {
        PropSync = 200,
        SettingSync = 201,
    }
    [HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.HandleRpc))]
    class RPCPatch
    {
        public static void Postfix([HarmonyArgument(0)] byte callId,[HarmonyArgument(1)] MessageReader reader)
        {
            var rpc = (RPC)callId;
            switch (rpc)
            {
                case RPC.PropSync:
                    byte id = reader.ReadByte();
                    PlayerControl player = null;
                    int idx = reader.ReadInt32();
                    player = PlayerControl.AllPlayerControls.ToArray().Where(pc => pc.PlayerId == id).FirstOrDefault();
                    RPCHandler.RPCPropSync(player, idx);
                    break;
                case RPC.SettingSync:
                    byte pid = reader.ReadByte();
                    PlayerControl p = null;
                    var hidingTime = reader.ReadInt32();
                    var missedKills = reader.ReadInt32();
                    var infection = reader.ReadBoolean();
                    p = PlayerControl.AllPlayerControls.ToArray().Where(pc => pc.PlayerId == pid).FirstOrDefault();
                    RPCHandler.RPCSettingSync(p, hidingTime, missedKills, infection);
                    break;
            }
        }

    }
}
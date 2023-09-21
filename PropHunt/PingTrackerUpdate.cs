using System;
using System.Text;
using HarmonyLib;

namespace PropHunt
{
    public class PingTracker_Update
    {

[HarmonyPatch(typeof(PingTracker),nameof(PingTracker.Update))]
        [HarmonyPostfix]
        public static void PingTrackerPatch(PingTracker __instance)
        {
            StringBuilder ping = new();
            ping.Append("\n<color=");
            if (AmongUsClient.Instance.Ping < 100)
            {
                ping.Append("#0000FF>");
            }
            else if (AmongUsClient.Instance.Ping < 300)
            {
                ping.Append("#ffff00>");
            }
            else if (AmongUsClient.Instance.Ping > 300)
            {
                ping.Append("#ff0000>");
            }
            else if (AmongUsClient.Instance.Ping > 500)
            {
                ping.Append("#008000");
            }
            ping.Append(string.Format(Language.GetMessage(StringOptions.Ping), AmongUsClient.Instance.Ping)).Append($"</color>\n<size=130%>Prop Hunt Reactivited</size> v{PropHunt.VersionString}\n<size=65%>By <color=ff0000>JeanAU</color>\n <size=65%> Original dev <color=#008000>ugackMiner53</color></size>");
            __instance.text.text = ping.ToString();
        }
    }
}
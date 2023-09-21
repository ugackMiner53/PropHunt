using System;
using HarmonyLib;
using UnityEngine;
using Hazel;

namespace PropHunt
{
    class CustomRoleSettings
    {
        static GameObject textObject;
        static GameObject toggleOption;
        static GameObject numberOption;

        static NumberOption hidingOption;
        static NumberOption maxMissOption;
        static ToggleOption infectionOption;



        [HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.Start))]
        [HarmonyPostfix]
        public static void PropOptionsMenuPatch(RolesSettingsMenu __instance)
        {
            // First time setup
            if (toggleOption == null && numberOption == null)
            {
                textObject = GameObject.Instantiate(GameObject.Find("Role Name").gameObject);
                textObject.GetComponent<TMPro.TextMeshPro>().text = Language.GetMessage(StringOptions.PropHunt);
                toggleOption = GameObject.Instantiate(__instance.AdvancedRolesSettings.GetComponentInChildren<ToggleOption>().gameObject);
                numberOption = GameObject.Instantiate(__instance.AdvancedRolesSettings.GetComponentInChildren<NumberOption>().gameObject);
                textObject.SetActive(false);
                toggleOption.SetActive(false);
                numberOption.SetActive(false);
            }
            // Remove the other role settings
            __instance.RoleChancesSettings.SetActive(false);
            __instance.AdvancedRolesSettings.SetActiveRecursively(false);
            __instance.AdvancedRolesSettings.SetActive(true);
            // Prop Hunt text
            GameObject textInstance = GameObject.Instantiate(textObject, __instance.AdvancedRolesSettings.transform);
            textInstance.transform.position = new Vector3(textInstance.transform.position.x - 2, textInstance.transform.position.y + 0.5f, textInstance.transform.position.z);
            textInstance.SetActive(true);
            // Hiding Option
            hidingOption = GameObject.Instantiate(numberOption, __instance.AdvancedRolesSettings.transform).GetComponent<NumberOption>();
            hidingOption.gameObject.SetActive(true);
            hidingOption.Title = StringNames.NoneLabel;
            hidingOption.Increment = 5;
            hidingOption.ValidRange = new FloatRange(5, 120);
            hidingOption.SuffixType = NumberSuffixes.Seconds;
            hidingOption.Value = PropHunt.hidingTime;
            hidingOption.transform.position = new Vector3(hidingOption.transform.position.x, hidingOption.transform.position.y - 0.5f, hidingOption.transform.position.z);
            hidingOption.TitleText.text =  Language.GetMessage(StringOptions.HidingTime);
            // Max Miss Option
            maxMissOption = GameObject.Instantiate(numberOption, __instance.AdvancedRolesSettings.transform).GetComponent<NumberOption>();
            maxMissOption.gameObject.SetActive(true);
            maxMissOption.Title = StringNames.NoneLabel;
            maxMissOption.Increment = 1;
            maxMissOption.ValidRange = new FloatRange(1, 35);
            maxMissOption.SuffixType = NumberSuffixes.None;
            maxMissOption.Value = PropHunt.maxMissedKills;
            maxMissOption.transform.position = new Vector3(maxMissOption.transform.position.x, maxMissOption.transform.position.y, maxMissOption.transform.position.z);
            maxMissOption.TitleText.text =  Language.GetMessage(StringOptions.MaxMisKill);
            // Infection Option
            infectionOption = GameObject.Instantiate(toggleOption, __instance.AdvancedRolesSettings.transform).GetComponent<ToggleOption>();
            infectionOption.gameObject.SetActive(true);
            infectionOption.Title = StringNames.NoneLabel;
            infectionOption.transform.position = new Vector3(infectionOption.transform.position.x, infectionOption.transform.position.y, infectionOption.transform.position.z);
            if ((PropHunt.infection && !infectionOption.GetBool()) || (!PropHunt.infection && infectionOption.GetBool()))
                infectionOption.Toggle();
            infectionOption.TitleText.text =  Language.GetMessage(StringOptions.Infection);
        }

          public static void SyncCustomSettings()
        {
            if (hidingOption && maxMissOption && infectionOption)
            {
                var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 
                (byte)PropHunt.RPC.SettingSync, SendOption.Reliable -1);
                writer.Write(PlayerControl.LocalPlayer.PlayerId);
                writer.Write(hidingOption.GetInt());
                writer.Write(maxMissOption.GetInt());
                writer.Write(infectionOption.GetBool());
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                PropHunt.RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, hidingOption.GetInt(), maxMissOption.GetInt(), infectionOption.GetBool());
            }
        }


        [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.ToHudString))]
        [HarmonyPrefix]
        public static void HudStringPatch(ref string __result)
        {
            if (hidingOption && maxMissOption && infectionOption)
            {
                __result += $"\n{ Language.GetMessage(StringOptions.HidingTime)}: {PropHunt.hidingTime}s\n{ Language.GetMessage(StringOptions.MaxMisKill)}: {PropHunt.maxMissedKills}\n{ Language.GetMessage(StringOptions.Infection)}: {(PropHunt.infection ? "On" : "Off")}";

                PropHunt.RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, hidingOption.GetInt(), maxMissOption.GetInt(), infectionOption.GetBool());
            }
          
     }
           // Sync Player
            [HarmonyPatch(typeof(AmongUsClient),nameof(AmongUsClient.OnGameJoined))]
            [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
            [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.ToHudString))]
            [HarmonyPostfix]
            public static void SyncCustomSettingsPatch()
            {
                SyncCustomSettings();
         }
    }
}

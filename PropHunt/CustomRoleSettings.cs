using HarmonyLib;
using UnityEngine;

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
                textObject.GetComponent<TMPro.TextMeshPro>().text = "Prop Hunt";
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
            hidingOption.Value = PropHuntPlugin.hidingTime;
            hidingOption.transform.position = new Vector3(hidingOption.transform.position.x, hidingOption.transform.position.y - 0.5f, hidingOption.transform.position.z);
            hidingOption.TitleText.text = "Hiding Time";
            // Max Miss Option
            maxMissOption = GameObject.Instantiate(numberOption, __instance.AdvancedRolesSettings.transform).GetComponent<NumberOption>();
            maxMissOption.gameObject.SetActive(true);
            maxMissOption.Title = StringNames.NoneLabel;
            maxMissOption.Increment = 1;
            maxMissOption.ValidRange = new FloatRange(1, 35);
            maxMissOption.SuffixType = NumberSuffixes.None;
            maxMissOption.Value = PropHuntPlugin.maxMissedKills;
            maxMissOption.transform.position = new Vector3(maxMissOption.transform.position.x, maxMissOption.transform.position.y, maxMissOption.transform.position.z);
            maxMissOption.TitleText.text = "Maximum Missed Kills";
            // Infection Option
            infectionOption = GameObject.Instantiate(toggleOption, __instance.AdvancedRolesSettings.transform).GetComponent<ToggleOption>();
            infectionOption.gameObject.SetActive(true);
            infectionOption.Title = StringNames.NoneLabel;
            infectionOption.transform.position = new Vector3(infectionOption.transform.position.x, infectionOption.transform.position.y, infectionOption.transform.position.z);
            if ((PropHuntPlugin.infection && !infectionOption.GetBool()) || (!PropHuntPlugin.infection && infectionOption.GetBool()))
                infectionOption.Toggle();
            infectionOption.TitleText.text = "Infection Mode";
        }


        [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.ToHudString))]
        [HarmonyPrefix]
        public static void SyncCustomSettings(GameOptionsData __instance)
        {
            if (hidingOption && maxMissOption && infectionOption)
            {
                PropHuntPlugin.RPCHandler.RPCSettingSync(PlayerControl.LocalPlayer, hidingOption.GetFloat(), maxMissOption.GetInt(), infectionOption.GetBool());
            }
        }
    }
}

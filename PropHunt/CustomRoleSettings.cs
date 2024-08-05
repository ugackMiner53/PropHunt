using HarmonyLib;
using TMPro;
using UnityEngine;
using Reactor.Utilities;

namespace PropHunt
{
    class CustomRoleSettings
    {
        [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.Start))]
        [HarmonyPostfix]
        static void CreatePropHuntPreset(GamePresetsTab __instance)
        {
            // if ()
            GameObject propHuntObject = GameObject.Instantiate(__instance.SecondPresetButton.gameObject);
            propHuntObject.name = "PropHuntButton";

            // Setup the correct components (they don't copy correctly)
            PassiveButton propHuntButton = propHuntObject.GetComponent<PassiveButton>();
            propHuntButton.buttonText = propHuntButton.transform.GetChild(0).GetComponent<TextMeshPro>();
            propHuntButton.activeSprites = propHuntObject.transform.GetChild(1).gameObject;
            propHuntButton.inactiveSprites = propHuntObject.transform.GetChild(2).gameObject;
            propHuntButton.selectedSprites = propHuntObject.transform.GetChild(3).gameObject;

            // Change button text
            propHuntButton.buttonText.text = "Prop Hunt";

            // Set the sprite of the button
            propHuntButton.activeSprites.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite = PropHuntPlugin.prophuntportraitTest;
            propHuntButton.inactiveSprites.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite = PropHuntPlugin.prophuntportraitTest;
            propHuntButton.selectedSprites.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite = PropHuntPlugin.prophuntportraitTest;
            Logger<PropHuntPlugin>.Debug("Should've changed sprite to " + PropHuntPlugin.prophuntportraitTest);
            


            // __instance.SecondPresetButton.gameObject
        }
    }
}

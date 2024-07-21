using HarmonyLib;

public static class GameModeMenuPatch 
{
    [HarmonyPatch(typeof(GameModeMenu), nameof(GameModeMenu.OnEnable))]
    public static void CreateButtonsPatch(GameModeMenu __instance) {
        // ChatLanguageButton chatLanguageButton = __instance.ButtonPool.Get<ChatLanguageButton>();
        // chatLanguageButton.transform.localPosition = new Vector3(num + (float)(num2 / 10) * 2.5f, 2f - (float)(num2 % 10) * 0.5f, 0f);
        // chatLanguageButton.Text.text = "Prop Hunt";
        // chatLanguageButton.Button.OnClick.RemoveAllListeners();
        // chatLanguageButton.Button.OnClick.AddListener(delegate {
        //     __instance.ChooseOption(entry);
        // });
        // chatLanguageButton.SetSelected((long)entry == (long)((ulong)gameMode));
        // this.controllerSelectable.Add(chatLanguageButton.Button);
        // if ((long)entry == (long)((ulong)gameMode))
        // {
        //     __instance.defaultButtonSelected = chatLanguageButton.Button;
        // }
    }
}
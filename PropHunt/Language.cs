// Some code from https://github.com/ModLaboratory/PropHunt-Plus/blob/master/PropHunt/Language.cs

using HarmonyLib;
using System.Collections.Generic;

namespace PropHunt
{ 
    public enum StringOptions 
    {
        PropHunt,
        HidingTime,
        MaxMisKill,
        Infection,
        Ping,
        RemainAttempt,
        Seeker,
        SeekerDescription,
        Prop,
        PropDescription,
        HidingTimeLeft,
        CmdHelp,
        SystemMessage,
        SeekerDead,
        PropDead,
        PropInfected,
        MeetingDisabled,
    }

    public static class Language
    {
        public static Dictionary<StringOptions, string> langDic = new();
        [HarmonyPatch(typeof(TranslationController),nameof(TranslationController.Initialize))]
        [HarmonyPostfix]
        public static void Init(TranslationController __instance)
        {
            langDic = GetLang(__instance.currentLanguage.languageID);
        }

        [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.SetLanguage))]
        [HarmonyPostfix]
        public static void SetLangPatch([HarmonyArgument(0)] TranslatedImageSet lang)
        {
            langDic = GetLang(lang.languageID);
        }

        public static string GetMessage(StringOptions options)
        {
            string result = "";
            try
            {
                result = langDic[options];
            }
            catch
            {
                result = "<ERR_GET_TRSLATION>" + options.ToString();
            }
            return result;
        }

        private static Dictionary<StringOptions, string> GetLang(SupportedLangs lang)
        {
            switch (lang)
            {
                default:
                case SupportedLangs.English:
                    return new()
                    {
                        [StringOptions.PropHunt] = "Prop Hunt",
                        [StringOptions.HidingTime] = "Hiding Time",
                        [StringOptions.MaxMisKill] = "Maximum Missed Kills",
                        [StringOptions.Infection] = "Infection Mode",
                        [StringOptions.Ping]="Ping: {0} ms",
                        [StringOptions.RemainAttempt]="Remaining Attempts: {0}",
                        [StringOptions.Seeker]="Seeker",
                        [StringOptions.SeekerDescription]= "Find and kill the props\nYour game will be unfrozen after {0} seconds",
                        [StringOptions.Prop]="Prop",
                        [StringOptions.PropDescription]= "Turn into props to hide from the seekers",
                        [StringOptions.HidingTimeLeft]="{0} seconds left for hiding!",
                        [StringOptions.CmdHelp]= "<b>How to play</b>:\n</b>R</b>: (Prop only) Turn into nearest task\n<b>Shift</b>: Noclip through walls\n<b>Note: Noclip is a temporary solution for getting stuck, not for hiding!</b>",
                        [StringOptions.SystemMessage]="System Message",
                        [StringOptions.SeekerDead]="Seeker {0} was dead!\n{1} Seeker(s) remaining, {2} Prop(s) remaining.",
                        [StringOptions.PropDead]= "Prop {0} was dead!\n{1} Seeker(s) remaining, {2} Prop(s) remaining.",
                        [StringOptions.PropInfected] = "Prop {0} was infected into seeker!\n{1} Seeker(s) remaining, {2} Prop(s) remaining.",
                        [StringOptions.MeetingDisabled] = "Meeting was disabled when playing Prop Hunt mode",
                    };
                    // Note : French its not yet translated error can be occured
                    case SupportedLangs.French:
                    return new()
                    {
                        [StringOptions.PropHunt] =   "Chasse aux accessoires",
                        [StringOptions.HidingTime] = "Temps de masquage",
                        [StringOptions.MaxMisKill] = "Nombre maximum de victimes manquées",
                        [StringOptions.Infection] = "Mode d'infection",
                        [StringOptions.Ping] = "Ping: {0} ms",
                        [StringOptions.RemainAttempt]= "Tentatives restantes : {0}",
                        [StringOptions.Seeker] = "Chercheur",
                        [StringOptions.SeekerDescription] = "Trouvez et tuez les Chasse aux accessoires\nVotre jeu sera dégelé après {0} secondes",
                        [StringOptions.Prop] = "accessoire",
                        [StringOptions.PropDescription] = "Transformez-les en accessoires pour les cacher aux chercheurs.",
                        [StringOptions.HidingTimeLeft] = "Il reste {0} secondes pour se cacher !",
                        [StringOptions.CmdHelp] = "<b>Comment Jouer ?</b>:\n</b>R</b>: (Prop uniquement) cloner la task la plus proche\n<b>Shift</b>: Traversé les murs\n<b>Noté : c'est une solution temporaire pour rester coincé, pas pour se cacher !</b>",
                        [StringOptions.SystemMessage] = "Message du système",
                        [StringOptions.SeekerDead] = "Le Chercheur {0} est mort ! {1} Chercheur(s) restant(s), {2} Chasse aux accessoires(s) restant(s).",
                        [StringOptions.PropDead] = "Chasse aux accessoires {0} est mort !\n{1} Chercheur(s) restant(s) {2} Chasse aux accessoire(s) restant(s).",
                        [StringOptions.PropInfected] = "Chasse aux accessoires {0} a été infecté par un chercheur!\n{1} Chercheur(s) restant(s), {2} Prop(s) restant(s).",
                        [StringOptions.MeetingDisabled] = "La réunion était désactivée lors du jeu en mode Prop Hunt",
                    };
            }
        }
    }
}
    



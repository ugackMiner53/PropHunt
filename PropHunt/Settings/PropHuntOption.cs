using AmongUs.GameOptions;

namespace PropHunt.Settings
{
    public class PropHuntOption
    {
        public byte Id;
        public string Name;
        public string[] AllValues;
        public byte Value = 0;
        public string Suffix = "";
        public StringNames StringName;
    }
}

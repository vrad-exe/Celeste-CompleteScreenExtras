namespace Celeste.Mod.CompleteScreenExtras;

[SettingName("modoptions_completescreenextras_title")]
public class CompleteScreenExtrasModuleSettings : EverestModuleSettings {

    [SettingName("modoptions_completescreenextras_animmode")]
    [SettingSubText("modoptions_completescreenextras_animmode_help")]
    public TextAnimModeType TextAnimMode { get; set; } = TextAnimModeType.Original;

    [SettingName("modoptions_completescreenextras_rainbowmode")]
    public TextRainbowModeType TextRainbowMode { get; set; } = TextRainbowModeType.Disabled;

    public enum TextAnimModeType
    {
        Disabled = 0,
        Original = 1,
        Delayed = 2
    }
    public enum TextRainbowModeType
    {
        Disabled = 0,
        FullClearOnly = 1,
        Always = 2
    }
}
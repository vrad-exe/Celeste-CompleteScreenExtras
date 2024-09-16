namespace Celeste.Mod.ChapterCompleteAdditions;

[SettingName("modoptions_chaptercompleteadditions_title")]
public class ChapterCompleteAdditionsModuleSettings : EverestModuleSettings {

    [SettingName("modoptions_chaptercompleteadditions_animmode")]
    [SettingSubText("modoptions_chaptercompleteadditions_animmode_help")]
    public TextAnimModeType TextAnimMode { get; set; } = TextAnimModeType.Original;

    [SettingName("modoptions_chaptercompleteadditions_rainbowmode")]
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
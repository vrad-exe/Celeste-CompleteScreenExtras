namespace Celeste.Mod.CompleteScreenExtras;

[SettingName("modoptions_completescreenextras_title")]
public class CompleteScreenExtrasModuleSettings : EverestModuleSettings {

	[SettingName("modoptions_completescreenextras_animlayerfix")]
	[SettingSubText("modoptions_completescreenextras_animlayerfix_help")]
	public bool TextAnimLayerFix { get; set; } = true;

	[SettingName("modoptions_completescreenextras_animdelay")]
	[SettingSubText("modoptions_completescreenextras_animdelay_help")]
	public float TextAnimDelay { get; set; } = 0.0f;

	[SettingName("modoptions_completescreenextras_rainbowmode")]
	public TextRainbowModeType TextRainbowMode { get; set; } = TextRainbowModeType.Disabled;

	public enum TextRainbowModeType
	{
		Disabled = 0,
		FullClearOnly = 1,
		Always = 2
	}
}
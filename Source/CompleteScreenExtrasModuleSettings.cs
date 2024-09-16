namespace Celeste.Mod.CompleteScreenExtras;

[SettingName("modoptions_completescreenextras_title")]
public class CompleteScreenExtrasModuleSettings : EverestModuleSettings {

	[SettingName("modoptions_completescreenextras_textanimlayerfix")]
	[SettingSubText("modoptions_completescreenextras_textanimlayerfix_help")]
	public bool TextAnimLayerFix { get; set; } = true;

	[SettingName("modoptions_completescreenextras_textanimdelay")]
	[SettingSubText("modoptions_completescreenextras_textanimdelay_help")]
	[SettingRange(0, 30)]
	public int TextAnimDelay { get; set; } = 0;

	[SettingName("modoptions_completescreenextras_textrainbowmode")]
	public TextRainbowModeType TextRainbowMode { get; set; } = TextRainbowModeType.Disabled;

	public enum TextRainbowModeType
	{
		Disabled = 0,
		FullClearOnly = 1,
		Always = 2
	}
}
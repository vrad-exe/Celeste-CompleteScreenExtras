using IL.Monocle;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Reflection;

namespace Celeste.Mod.CompleteScreenExtras;

public class CompleteScreenExtrasModule : EverestModule {
	public static CompleteScreenExtrasModule Instance { get; private set; }

	public override Type SettingsType => typeof(CompleteScreenExtrasModuleSettings);
	public static CompleteScreenExtrasModuleSettings Settings => (CompleteScreenExtrasModuleSettings) Instance._Settings;

	// Name that we display in the logger
	public const string LOGGER_NAME = "CompleteScreenExtras";

	public CompleteScreenExtrasModule() {
		Instance = this;
#if DEBUG
		// debug builds use verbose logging
		Logger.SetLogLevel(LOGGER_NAME, LogLevel.Verbose);
#else
		// release builds use info logging to reduce spam in log files
		Logger.SetLogLevel(LoggerName, LogLevel.Info);
#endif
	}

	public override void Load() {
		Logger.Log(LOGGER_NAME, "Computer, activate IL hooks");
		IL.Celeste.AreaComplete.ctor += Hook_AreaComplete_Ctor;
		IL.Celeste.CompleteRenderer.RenderContent += Hook_CompleteRenderer_RenderContent;
	}

	public override void Unload() {
		Logger.Log(LOGGER_NAME, "Computer, deactivate IL hooks");
		IL.Celeste.AreaComplete.ctor -= Hook_AreaComplete_Ctor;
		IL.Celeste.CompleteRenderer.RenderContent -= Hook_CompleteRenderer_RenderContent;
	}

	// ----- RAINBOW TEXT -----

	private static bool ShouldUseChapterRainbow(bool isFullClear)
	{
		return Settings.TextRainbowMode == CompleteScreenExtrasModuleSettings.TextRainbowModeType.Always
			|| (Settings.TextRainbowMode == CompleteScreenExtrasModuleSettings.TextRainbowModeType.FullClearOnly && isFullClear);
	}

	private static void SetTextDelays(AreaCompleteTitle title)
	{
		foreach (AreaCompleteTitle.Letter letter in title.letters)
		{
			// Delay is in 1/10 seconds because everest doesn't support float settings
			letter.delay += (Settings.TextAnimDelay / 10.0f);
		}
	}

	private void Hook_AreaComplete_Ctor(ILContext il)
	{
		ILCursor cursor = new ILCursor(il);

		MethodInfo session_get_FullClear = typeof(Session).GetMethod("get_FullClear", BindingFlags.Public | BindingFlags.Instance);
		FieldInfo AreaComplete_title = typeof(AreaComplete).GetField("title", BindingFlags.NonPublic | BindingFlags.Instance);

		if (session_get_FullClear == null || AreaComplete_title == null)
		{
			Logger.Log(LogLevel.Warn, LOGGER_NAME,
				"Couldn't find required fields/methods for AreaComplete::Ctor hook! Did the game update, or is another mod conflicting?");
			return;
		}

		// jump to where AreaCompleteTitle is instantiated
		if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchNewobj(typeof(AreaCompleteTitle)) ))
		{
			Logger.Log(LOGGER_NAME, $"Patching rainbow text at {cursor.Index} in CIL code for {cursor.Method.FullName}!");

			// pop the original 0 off the stack, then get the value from the function
			cursor.Emit(OpCodes.Pop);
			cursor.Emit(OpCodes.Ldarg_1);
			cursor.Emit(OpCodes.Callvirt, session_get_FullClear);
			cursor.EmitDelegate<Func<bool, bool>>(ShouldUseChapterRainbow);

			// Delay all letters by user specified amount
			cursor.Index += 2;
			cursor.Emit(OpCodes.Ldarg_0);
			cursor.Emit(OpCodes.Ldfld, AreaComplete_title);
			cursor.EmitDelegate(SetTextDelays);
		}
	}


	// ----- ANIMATED TEXT -----

	// easier to hook in these functions instead of having it grab the settings directly
	private static bool ShouldFixTextLayering()
	{
		return Settings.TextAnimLayerFix == true;
	}

	private void Hook_CompleteRenderer_RenderContent(ILContext il)
	{
		ILCursor cursor = new ILCursor(il);

		FieldInfo CompleteRenderer_RenderPostUI = typeof(CompleteRenderer).GetField("RenderPostUI", BindingFlags.Public | BindingFlags.Instance);
		MethodInfo HiresRenderer_EndRender = typeof(HiresRenderer).GetMethod("EndRender", BindingFlags.Public | BindingFlags.Static);
		MethodInfo System_Action_Invoke = typeof(System.Action).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);

		if (CompleteRenderer_RenderPostUI == null || HiresRenderer_EndRender == null || System_Action_Invoke == null) 
		{
			Logger.Log(LogLevel.Warn, LOGGER_NAME,
				"Couldn't find required fields/methods for CompleteRenderer::RenderContent hook! Did the game update, or is another mod conflicting?");
			return;
		}

		// We need to swap the order in which it draws the UI elements
		// First, add a check to skip drawing the text at the original point
		if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld(CompleteRenderer_RenderPostUI)))
		{
			// go after the following brfalse.s
			cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Brfalse_S);
			Logger.Log(LOGGER_NAME, $"Patching chapter text drawing, part 1: at {cursor.Index} in CIL code for {cursor.Method.FullName}!");

			// Add check for if the setting is in "original" mode
			cursor.EmitDelegate<Func<bool>>(ShouldFixTextLayering);

			ILLabel target = cursor.DefineLabel();
			cursor.Emit(OpCodes.Brtrue_S, target);
			cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt(System_Action_Invoke)
			).MarkLabel(target);
		}

		ILLabel fadeBranchTarget = null;

		// Now we need to move the text draw to the end of the function, after the fade stuff has been done
		if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall(HiresRenderer_EndRender)))
		{
			Logger.Log(LOGGER_NAME, $"Patching chapter text drawing, part 2: at {cursor.Index} in CIL code for {cursor.Method.FullName}!");

			// This is where we want the fade to branch to
			fadeBranchTarget = cursor.DefineLabel();
			cursor.MarkLabel(fadeBranchTarget);

			// Add the same "original" check, only this time we branch if false
			cursor.EmitDelegate<Func<bool>>(ShouldFixTextLayering);

			ILLabel target = cursor.DefineLabel();
			cursor.Emit(OpCodes.Brfalse_S, target);

			// Reinsert the original instructions for drawing the text
			// check if RenderPostUI is not null
			cursor.Emit(OpCodes.Ldarg_0);
			cursor.Emit(OpCodes.Ldfld, CompleteRenderer_RenderPostUI);
			cursor.Emit(OpCodes.Brfalse_S, target);
			// call it!
			cursor.Emit(OpCodes.Ldarg_0);
			cursor.Emit(OpCodes.Ldfld, CompleteRenderer_RenderPostUI);
			cursor.Emit(OpCodes.Callvirt, System_Action_Invoke);

			// Finish off by setting the branch target
			cursor.GotoNext(MoveType.Before, instr => instr.MatchCall(HiresRenderer_EndRender)
			).MarkLabel(target);
		}

		// Finally, go back and update the fadeAlpha > 0 branch to jump to our code instead of straight to EndRender
		// Otherwise all the text disappears once the fadein finishes
		if (cursor.TryGotoPrev(MoveType.Before, instr => instr.OpCode == OpCodes.Ble_Un_S) && fadeBranchTarget != null)
		{
			cursor.Next.Operand = fadeBranchTarget;
		}
	}
}
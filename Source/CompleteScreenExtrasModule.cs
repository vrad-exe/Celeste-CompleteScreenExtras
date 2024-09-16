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

    public const string LoggerName = "CompleteScreenExtras";

    public CompleteScreenExtrasModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(LoggerName, LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(LoggerName, LogLevel.Info);
#endif
    }

    public override void Load() {
        Logger.Log(LoggerName, "Computer, activate IL hooks");
        IL.Celeste.AreaComplete.ctor += Hook_AreaComplete_Ctor;
        IL.Celeste.CompleteRenderer.RenderContent += Hook_CompleteRenderer_RenderContent;
    }

    public override void Unload() {
        Logger.Log(LoggerName, "Computer, deactivate IL hooks");
        IL.Celeste.AreaComplete.ctor -= Hook_AreaComplete_Ctor;
        IL.Celeste.CompleteRenderer.RenderContent -= Hook_CompleteRenderer_RenderContent;
    }

    // copied from extended variants
    private MethodReference SeekReferenceToMethod(ILContext il, string methodName, OpCode opcode)
    {
        ILCursor cursor = new ILCursor(il);
        if (cursor.TryGotoNext(MoveType.Before, instr => instr.OpCode == opcode && ((MethodReference)instr.Operand).Name.Contains(methodName)))
        {
            return (MethodReference)cursor.Next.Operand;
        }
        return null;
    }

    private FieldReference SeekReferenceToField(ILContext il, string fieldName, OpCode opcode)
    {
        ILCursor cursor = new ILCursor(il);
        if (cursor.TryGotoNext(MoveType.Before, instr => instr.OpCode == opcode && ((FieldReference)instr.Operand).Name.Contains(fieldName)))
        {
            return (FieldReference)cursor.Next.Operand;
        }
        return null;
    }

    // ----- RAINBOW TEXT -----

    private static bool ShouldUseChapterRainbow(bool isFullClear)
    {
        return Settings.TextRainbowMode == CompleteScreenExtrasModuleSettings.TextRainbowModeType.Always
            || (Settings.TextRainbowMode == CompleteScreenExtrasModuleSettings.TextRainbowModeType.FullClearOnly && isFullClear);
    }

    private void Hook_AreaComplete_Ctor(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        MethodReference session_get_FullClear = SeekReferenceToMethod(il, "get_FullClear", OpCodes.Callvirt);

        // jump to where AreaCompleteTitle is instantiated
        if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchNewobj(typeof(AreaCompleteTitle)) ))
        {
            Logger.Log(LoggerName, $"Patching rainbow text at {cursor.Index} in CIL code for {cursor.Method.FullName}!");

            // pop the original 0 off the stack, then get the value from the function
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Callvirt, session_get_FullClear);
            cursor.EmitDelegate<Func<bool, bool>>(ShouldUseChapterRainbow);
        }
    }


    // ----- ANIMATED TEXT -----

    // easier to hook in these functions instead of having it grab the settings directly
    private static bool ShouldAnimateOriginal()
    {
        return Settings.TextAnimMode == CompleteScreenExtrasModuleSettings.TextAnimModeType.Original;
    }
    private static bool ShouldAnimateDelayed()
    {
        return Settings.TextAnimMode == CompleteScreenExtrasModuleSettings.TextAnimModeType.Delayed;
    }

    private void Hook_CompleteRenderer_RenderContent(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        FieldReference CompleteRenderer_RenderPostUI = SeekReferenceToField(il, "RenderPostUI", OpCodes.Ldfld);
        MethodReference HiresRenderer_EndRender = SeekReferenceToMethod(il, "EndRender", OpCodes.Call);
        MethodReference System_Action_Invoke = SeekReferenceToMethod(il, "Invoke", OpCodes.Callvirt);

        // We need to swap the order in which it draws the UI elements
        // First, add a check to skip drawing the text at the original point
        if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld(CompleteRenderer_RenderPostUI)))
        {
            // go after the following brfalse.s
            cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Brfalse_S);
            Logger.Log(LoggerName, $"Patching chapter text drawing, part 1: at {cursor.Index} in CIL code for {cursor.Method.FullName}!");

            // Add check for if the setting is in "original" mode
            cursor.EmitDelegate<Func<bool>>(ShouldAnimateOriginal);

            ILLabel target = cursor.DefineLabel();
            cursor.Emit(OpCodes.Brtrue_S, target);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt(System_Action_Invoke)
            ).MarkLabel(target);
        }

        ILLabel fadeBranchTarget = null;

        // Now we need to move the text draw to the end of the function, after the fade stuff has been done
        if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall(HiresRenderer_EndRender)))
        {
            Logger.Log(LoggerName, $"Patching chapter text drawing, part 2: at {cursor.Index} in CIL code for {cursor.Method.FullName}!");

            // This is where we want the fade to branch to
            fadeBranchTarget = cursor.DefineLabel();
            cursor.MarkLabel(fadeBranchTarget);

            // Add the same "original" check, only this time we branch if false
            cursor.EmitDelegate<Func<bool>>(ShouldAnimateOriginal);

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
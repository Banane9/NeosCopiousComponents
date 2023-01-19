using BaseX;
using FrooxEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace CopiousComponents
{
    [HarmonyPatch(typeof(ComponentCloneTip))]
    internal static class ComponentCloneTipPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnAwake")]
        private static void OnAwakePostfix(ComponentCloneTip __instance)
        {
            __instance.EnsureSingleInstance.Value = CopiousComponents.EnsureSingleInstanceDefault;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ComponentCloneTip.OnSecondaryPress))]
        private static bool OnSecondaryPressPrefix(ComponentCloneTip __instance, SyncRef<Slot> ____templateRoot, SyncRef<TextRenderer> ____label)
        {
            var grabber = __instance.ActiveTool.Grabber;
            if (!grabber.IsHoldingObjects || !__instance.AllowPickup || grabber.HolderSlot.GetComponentInChildren<ReferenceProxy>()?.Reference is not SyncRef reference)
                return false;

            // Add undo steps?
            switch (reference.Target)
            {
                case Component component:
                    var tempSlot = __instance.Slot.AddSlot("Template");
                    tempSlot.ActiveSelf = false;

                    CopiousComponents.DuplicateComponents(tempSlot, new List<Component> { component }, false);

                    ____templateRoot.Target?.Destroy();
                    ____templateRoot.Target = tempSlot;
                    ____label.Target.Text.Value = component.GetType().GetNiceName();
                    break;

                case Slot slot:
                    var dupSlot = __instance.Slot.AddSlot("Template - " + slot.Name);
                    dupSlot.ActiveSelf = false;

                    CopiousComponents.DuplicateComponents(dupSlot, slot.Components.ToList(), false);

                    ____templateRoot.Target?.Destroy();
                    ____templateRoot.Target = dupSlot;
                    ____label.Target.Text.Value = slot.Name;
                    break;
            }

            return false;
        }

        [HarmonyTranspiler]
        [HarmonyPatch("PlaceOn")]
        private static IEnumerable<CodeInstruction> PlaceOnTranspiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var duplicateComponentsMethod = typeof(Slot).GetMethod("DuplicateComponents", new[] { typeof(List<Component>), typeof(bool) });
            var sneakyDuplicateComponentsMethod = typeof(CopiousComponents).GetMethod(nameof(CopiousComponents.DuplicateComponents), AccessTools.allDeclared);

            foreach (var instruction in codeInstructions)
            {
                if (instruction.Calls(duplicateComponentsMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, sneakyDuplicateComponentsMethod);
                    continue;
                }

                yield return instruction;
            }
        }
    }
}
using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopiousComponents
{
    [HarmonyPatch(typeof(SlotRecord))]
    internal static class SlotRecordPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SlotRecord.TryReceive))]
        private static bool TryReceivePrefix(SlotRecord __instance, IEnumerable<IGrabbable> items, ref bool __result)
        {
            var targetSlot = __instance.TargetSlot.Target;
            __result = false;

            var slots = Pool.BorrowHashSet<Slot>();
            var components = Pool.BorrowHashSet<Component>();

            foreach (var item in items)
            {
                foreach (var referenceProxy in item.Slot.GetComponentsInChildren<ReferenceProxy>())
                {
                    switch (referenceProxy.Reference.Target)
                    {
                        case Slot slot when slot != targetSlot:
                            slots.Add(slot);
                            break;

                        case Component component when component.Slot != targetSlot:
                            components.Add(component);
                            break;

                        default:
                            break;
                    }
                }
            }

            if (slots.Count > 0)
            {
                __result = true;
                __instance.World.BeginUndoBatch("Reparents Slots");

                foreach (var slot in slots)
                {
                    slot.CreateTransformUndoState(parent: true);
                    slot.SetParent(targetSlot);
                }

                __instance.World.EndUndoBatch();
            }

            if (components.Count > 0)
            {
                __result = true;
                __instance.World.BeginUndoBatch($"{(DevToolTipPatches.MoveComponents ? "Moved" : "Copied")} Components");

                var values = Pool.BorrowList<object>();
                var sources = Pool.BorrowList<IWorldElement>();
                var replacements = Pool.BorrowList<IWorldElement>();

                foreach (var oldComponent in components)
                {
                    for (var i = 0; i < oldComponent.SyncMemberCount; ++i)
                    {
                        if (oldComponent.GetSyncMember(i) is IField field)
                        {
                            values.Add(field.BoxedValue);
                            sources.Add(field);

                            if (!field.IsDriven)
                                field.BoxedValue = field.ValueType.GetDefaultValue();
                        }
                    }

                    var newComponent = targetSlot.AttachComponent(oldComponent.GetType(), false);
                    newComponent.CreateSpawnUndoPoint();

                    for (var i = 0; i < newComponent.SyncMemberCount; ++i)
                    {
                        if (newComponent.GetSyncMember(i) is IField field)
                        {
                            field.BoxedValue = values[i];
                            replacements.Add(field);
                        }
                    }

                    if (DevToolTipPatches.MoveComponents)
                    {
                        sources.Add(oldComponent);
                        replacements.Add(newComponent);

                        __instance.World.ReplaceReferenceTargets(Enumerable.Range(0, replacements.Count).ToDictionary(i => sources[i], i => replacements[i]), true);

                        oldComponent.UndoableDestroy();
                    }

                    values.Clear();
                    sources.Clear();
                    replacements.Clear();
                }

                Pool.Return(ref values);
                __instance.World.EndUndoBatch();
            }

            Pool.Return(ref slots);
            Pool.Return(ref components);

            return false;
        }
    }
}
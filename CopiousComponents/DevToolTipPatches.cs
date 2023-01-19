using BaseX;
using FrooxEngine;
using FrooxEngine.LogiX.WorldModel;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static NeosAssets.Materials.FreePBR;

namespace CopiousComponents
{
    [HarmonyPatch(typeof(DevToolTip))]
    internal static class DevToolTipPatches
    {
        private static readonly color copyColor = new(0.3f, 1f, 0.4f);
        private static readonly color moveColor = color.White;
        private static readonly ConditionalWeakTable<Slot, DynamicValueVariable<bool>> moveToggleFields = new();
        private static bool moveComponents = CopiousComponents.MoveComponents;

        public static bool MoveComponents
        {
            get => moveComponents;

            private set
            {
                moveComponents = value;

                foreach (var world in Engine.Current.WorldManager.Worlds)
                {
                    var userRootSlot = world.LocalUser.Root.Slot;

                    if (moveToggleFields.TryGetValue(userRootSlot, out var field))
                    {
                        if (!field.IsDestroyed)
                        {
                            field.Value.Value = value;
                            continue;
                        }

                        moveToggleFields.Remove(userRootSlot);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DevToolTip.GenerateMenuItems))]
        private static void GenerateMenuItemsPostfix(DevToolTip __instance, ContextMenu menu)
        {
            menu.AddToggleItem(
                __instance.World.GetToggleField(),
                "Move Components", "Copy Components",
                moveColor, copyColor,
                NeosAssets.Common.Icons.Location, NeosAssets.Graphics.Icons.Item.Duplicate);
        }

        private static IField<bool> GetToggleField(this World world)
        {
            var userRootSlot = world.LocalUser.Root.Slot;

            if (!moveToggleFields.TryGetValue(userRootSlot, out var field) || field.IsDestroyed)
            {
                moveToggleFields.Remove(userRootSlot);

                field = world.LocalUser.Root.Slot.AttachComponent<DynamicValueVariable<bool>>();
                field.VariableName.Value = "User/CopiousComponents.MoveComponents";
                field.Persistent = false;

                field.Value.Value = MoveComponents;
                field.Value.OnValueChange += UpdateMoveComponents;

                moveToggleFields.Add(userRootSlot, field);
            }

            return field.Value;
        }

        private static void UpdateMoveComponents(SyncField<bool> field)
                    => MoveComponents = field.Value;
    }
}
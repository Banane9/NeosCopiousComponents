using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;

namespace CopiousComponents
{
    public class CopiousComponents : NeosMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnsureSingleInstanceDefaultKey = new ModConfigurationKey<bool>("EnsureSingleInstanceDefault", "The default value for Ensure Single Instance on the Component Clone Tip.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> GenerateMoveComponentsToggleMenuEntryKey = new ModConfigurationKey<bool>("GenerateMoveComponentsToggleMenuEntry", "Generate the moving components toggle in the DevTip Context Menu.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> MoveComponentsKey = new ModConfigurationKey<bool>("MoveComponents", "The default value for moving components rather than copying. Toggleable in the DevTip Context Menu.", () => true);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosCopiousComponents";
        public override string Name => "CopiousComponents";
        public override string Version => "1.0.0";

        internal static bool EnsureSingleInstanceDefault => Config.GetValue(EnsureSingleInstanceDefaultKey);
        internal static bool GenerateMoveComponentsToggleMenuEntry => Config.GetValue(GenerateMoveComponentsToggleMenuEntryKey);
        internal static bool MoveComponents => Config.GetValue(MoveComponentsKey);

        public override void OnEngineInit()
        {
            var harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();
        }

        internal static List<Component> DuplicateComponents(Slot target, List<Component> sourceComponents, bool breakExternalReferences)
            => SneakyWorker.DuplicateComponents(target, sourceComponents, breakExternalReferences);

        private abstract class SneakyWorker : Worker
        {
            public static List<Component> DuplicateComponents(Slot target, List<Component> sourceComponents, bool breakExternalReferences)
            {
                var internalRefs = new InternalReferences();
                var breakRefs = Pool.BorrowHashSet<ISyncRef>();

                var internalHierarchy = Pool.BorrowHashSet<Slot>();
                foreach (var sourceComponent in sourceComponents)
                    internalHierarchy.Add(sourceComponent.Slot);

                var duplicates = new List<Component>(sourceComponents.Count());

                foreach (var sourceComponent in sourceComponents)
                {
                    new Traverse(sourceComponent.Slot).Method("CollectInternalReferences", sourceComponent.Slot, sourceComponent, internalRefs, breakRefs, internalHierarchy).GetValue();
                }

                if (!breakExternalReferences)
                    breakRefs.Clear();

                var initInfo = new Traverse(target).Field<WorkerInitInfo>("InitInfo").Value;
                foreach (var slot in internalHierarchy)
                {
                    internalRefs.RegisterCopy(slot, target);
                    for (var i = 0; i < target.SyncMemberCount; ++i)
                    {
                        if (!initInfo.syncMemberDontCopy[i] && slot.GetSyncMember(i) is not WorkerBag<Component>)
                        {
                            internalRefs.RegisterCopy(slot.GetSyncMember(i), target.GetSyncMember(i));
                        }
                    }
                }

                foreach (var sourceComponent in sourceComponents)
                {
                    var newComponent = target.AttachComponent(sourceComponent.GetType(), runOnAttachBehavior: false);
                    internalRefs.RegisterCopy(sourceComponent, newComponent);

                    newComponent.CopyValues(sourceComponent, (from, to) => MemberCopy(from, to, internalRefs, breakRefs));

                    duplicates.Add(newComponent);
                }

                internalRefs.TransferReferences(preserveMissingTargets: true);

                foreach (var duplicate in duplicates)
                {
                    new Traverse(duplicate).Method("RunDuplicate").GetValue();
                }

                internalRefs.Dispose();
                Pool.Return(ref breakRefs);
                Pool.Return(ref internalHierarchy);

                return duplicates;
            }
        }
    }
}
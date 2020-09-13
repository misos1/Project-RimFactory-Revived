﻿using ProjectRimFactory.Common;
using ProjectRimFactory.Common.HarmonyPatches;
using ProjectRimFactory.SAL3.UI;
using ProjectRimFactory.Storage.Editables;
using ProjectRimFactory.Storage.UI;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace ProjectRimFactory.Storage
{
    [StaticConstructorOnStartup]
    public class Building_StorageUnitIOPort : Building_Storage , IForbidPawnInputItem
    {
        public static readonly Texture2D CargoPlatformTex = ContentFinder<Texture2D>.Get("Storage/CargoPlatform");
        public static readonly Texture2D IOModeTex = ContentFinder<Texture2D>.Get("UI/IoIcon");

        public StorageIOMode mode;
        Building_MassStorageUnit boundStorageUnit;
        protected StorageSettings outputStoreSettings;
        private OutputSettings outputSettings;

        CompPowerTrader powerComp;

        public override Graphic Graphic => this.IOMode == StorageIOMode.Input ?
            base.Graphic.GetColoredVersion(base.Graphic.Shader, this.def.GetModExtension<DefModExtension_StorageUnitIOPortColor>().inColor, Color.white) :
            base.Graphic.GetColoredVersion(base.Graphic.Shader, this.def.GetModExtension<DefModExtension_StorageUnitIOPortColor>().outColor, Color.white);

        public StorageIOMode IOMode
        {
            get
            {
                return mode;
            }
            set
            {
                if (mode == value) return;
                mode = value;
                Notify_NeedRefresh();
            }
        }

        public Building_MassStorageUnit BoundStorageUnit
        {
            get
            {
                return boundStorageUnit;
            }
            set
            {
                boundStorageUnit?.DeregisterPort(this);
                boundStorageUnit = value;
                value?.RegisterPort(this);
                Notify_NeedRefresh();
            }
        }

        protected OutputSettings OutputSettings
        {
            get
            {
                if (outputSettings == null)
                {
                    outputSettings = new OutputSettings("IOPort_Minimum_UseTooltip", "IOPort_Maximum_UseTooltip");
                }
                return outputSettings;
            }
            set
            {
                outputSettings = value;
            }
        }

        //
        public bool ForbidPawnInput {
            get
            {

                //maybe we should cash currentItem
                Thing currentItem = Position.GetFirstItem(Map);
                if (currentItem != null && IOMode == StorageIOMode.Output && outputSettings.useMax)
                {
                    return  OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit) <= 0;
                }
                return false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mode, "mode");
            Scribe_References.Look(ref boundStorageUnit, "boundStorageUnit");
            Scribe_Deep.Look(ref outputStoreSettings, "outputStoreSettings", this);
            Scribe_Deep.Look(ref outputSettings, "outputSettings", "IOPort_Minimum_UseTooltip", "IOPort_Maximum_UseTooltip");
        }
        public override string GetInspectString()
        {
            if (OutputSettings.useMin && OutputSettings.useMax) return base.GetInspectString() + "\n" + "IOPort_Minimum".Translate(OutputSettings.min) + "\n" + "IOPort_Maximum".Translate(OutputSettings.max);
            else if (OutputSettings.useMin && !OutputSettings.useMax) return base.GetInspectString() + "\n" + "IOPort_Minimum".Translate(OutputSettings.min);
            else if (!OutputSettings.useMin && OutputSettings.useMax) return base.GetInspectString() + "\n" + "IOPort_Maximum".Translate(OutputSettings.max);
            else return base.GetInspectString();
        }


        public override void PostMake()
        {
            base.PostMake();
            powerComp = GetComp<CompPowerTrader>();
            outputStoreSettings = new StorageSettings(this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        protected override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == CompPowerTrader.PowerTurnedOnSignal)
            {
                Notify_NeedRefresh();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn();
            boundStorageUnit?.DeregisterPort(this);
        }

        public void Notify_NeedRefresh()
        {
            RefreshStoreSettings();
            switch (IOMode)
            {
                case StorageIOMode.Input:
                    RefreshInput();
                    break;
                case StorageIOMode.Output:
                    RefreshOutput();
                    break;
            }
        }

        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            if (mode == StorageIOMode.Input)
            {
                RefreshInput();
            }
        }

        public override void Notify_LostThing(Thing newItem)
        {
            base.Notify_LostThing(newItem);
            if (mode == StorageIOMode.Output)
            {
                RefreshOutput();
            }
        }


        public override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(10))
            {
                Notify_NeedRefresh();
            }
        }

        public void RefreshStoreSettings()
        {
            if (mode == StorageIOMode.Output)
            {
                settings = outputStoreSettings;
                if (boundStorageUnit != null)
                {
                    settings.Priority = boundStorageUnit.settings.Priority;
                }
            }
            else if (boundStorageUnit != null)
            {
                settings = boundStorageUnit.settings;
            }
            else
            {
                settings = new StorageSettings(this);
            }
        }

        public void RefreshInput()
        {
            if (powerComp.PowerOn)
            {
                Thing item = Position.GetFirstItem(Map);
                if (mode == StorageIOMode.Input && item != null && boundStorageUnit != null && boundStorageUnit.settings.AllowedToAccept(item) && boundStorageUnit.CanReceiveIO && boundStorageUnit.CanStoreMoreItems)
                {
                    foreach (IntVec3 cell in boundStorageUnit.AllSlotCells())
                    {
                        if (cell.GetFirstItem(Map) == null)
                        {
                            boundStorageUnit.RegisterNewItem(item);
                            break;
                        }
                    }
                }
            }
        }

        protected IEnumerable<Thing> ItemsThatSatisfyMin(List<Thing> itemCandidates, Thing currentItem)
        {
            if (currentItem != null)
            {
                IEnumerable<Thing> stackableCandidates = itemCandidates.Where(t => currentItem.CanStackWith(t));
                return OutputSettings.SatisfiesMin(stackableCandidates.Sum(t => t.stackCount) + currentItem.stackCount) ? stackableCandidates : Enumerable.Empty<Thing>();
            }
            return itemCandidates
                .GroupBy(t => t.def)
                .FirstOrDefault(g => OutputSettings.SatisfiesMin(g.Sum(t => t.stackCount))) ?? Enumerable.Empty<Thing>();
        }

        protected void RefreshOutput() //
        {
            if (powerComp.PowerOn)
            {
                Thing currentItem = Position.GetFirstItem(Map);
                bool storageSlotAvailable = currentItem == null || (settings.AllowedToAccept(currentItem) &&
                                                                    OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit));
                if (boundStorageUnit != null && boundStorageUnit.CanReceiveIO)
                {
                    if (storageSlotAvailable && OutputSettings.min <= OutputSettings.max)
                    {
                        List<Thing> itemCandidates = new List<Thing>(from Thing t in boundStorageUnit.StoredItems where settings.AllowedToAccept(t) select t); // ToList very important - evaluates enumerable
                        if (ItemsThatSatisfyMin(itemCandidates, currentItem).Any())
                        {
                            foreach (Thing item in itemCandidates)
                            {
                                if (currentItem != null)
                                {
                                    if (currentItem.CanStackWith(item))
                                    {
                                        int count = Math.Min(item.stackCount, OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit));
                                        if (count > 0)
                                        {
                                            currentItem.TryAbsorbStack(item.SplitOff(count), true);
                                        }
                                    }
                                }
                                else
                                {
                                    int count = OutputSettings.CountNeededToReachMax(0, item.stackCount);
                                    if (count > 0)
                                    {
                                        currentItem = GenSpawn.Spawn(item.SplitOff(count), Position, Map);
                                    }
                                }
                                if (currentItem != null && !OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit))
                                {
                                    break;
                                }
                            }
                        }
                    }
                    //Transfre a item back if it is either too few or disallowed
                    if (currentItem != null && (!settings.AllowedToAccept(currentItem) || !OutputSettings.SatisfiesMin(currentItem.stackCount)) && boundStorageUnit.settings.AllowedToAccept(currentItem))
                    {
                        boundStorageUnit.RegisterNewItem(currentItem);
                    }
                    //Transfer the diffrence back if it is too much
                    if (currentItem != null && (!OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit) && boundStorageUnit.settings.AllowedToAccept(currentItem)))
                    {
                        int splitCount = -OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit);
                        if (splitCount > 0)
                        {
                            boundStorageUnit.RegisterNewItem(currentItem.SplitOff(splitCount));
                        }
                    }
                }
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            yield return new Command_Action()
            {
                defaultLabel = "PRFIOMode".Translate() + ": " + (IOMode == StorageIOMode.Input ? "PRFIOInput".Translate() : "PRFIOOutput".Translate()),
                action = () =>
                {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                    {
                        new FloatMenuOption("PRFIOInput".Translate(), () => SelectedPorts().ToList().ForEach(p => p.IOMode = StorageIOMode.Input)),
                        new FloatMenuOption("PRFIOOutput".Translate(), () => SelectedPorts().ToList().ForEach(p => p.IOMode = StorageIOMode.Output))
                    }));
                },
                icon = IOModeTex
            };
            yield return new Command_Action()
            {
                defaultLabel = "PRFBoundStorageBuilding".Translate() + ": " + (boundStorageUnit?.LabelCap ?? "NoneBrackets".Translate()),
                action = () =>
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>(
                        from Building_MassStorageUnit b in Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_MassStorageUnit>()
                        where b.def.GetModExtension<DefModExtension_CanUseStorageIOPorts>() != null
                        select new FloatMenuOption(b.LabelCap, () => SelectedPorts().ToList().ForEach(p => p.BoundStorageUnit = b))
                    );
                    if (list.Count == 0)
                    {
                        list.Add(new FloatMenuOption("NoneBrackets".Translate(), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                },
                icon = CargoPlatformTex
            };
            if (IOMode == StorageIOMode.Output)
            {
                yield return new Command_Action()
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel"),
                    defaultLabel = "PRFIOOutputSettings".Translate(),
                    action = () => Find.WindowStack.Add(new Dialog_OutputMinMax(OutputSettings, () => SelectedPorts().Where(p => p.IOMode == StorageIOMode.Output).ToList().ForEach(p => this.OutputSettings.Copy(p.OutputSettings))))
                };
            }
        }

        private IEnumerable<Building_StorageUnitIOPort> SelectedPorts()
        {
            var l = Find.Selector.SelectedObjects.Where(o => o is Building_StorageUnitIOPort).Select(o => (Building_StorageUnitIOPort)o).ToList();
            if (!l.Contains(this))
            {
                l.Add(this);
            }
            return l;
        }

        public void OutputItem(Thing thing)
        {
            if (this.boundStorageUnit?.CanReceiveIO ?? false)
            {
                Thing currentItem = Position.GetFirstItem(Map);
                if (currentItem == null)
                {
                    if (this.settings.AllowedToAccept(thing) && OutputSettings.SatisfiesMin(thing.stackCount))
                    {
                        GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), this.Position, this.Map, ThingPlaceMode.Near, null,
                                               delegate (IntVec3 pos) {
                                                   // Can place here or anywhere near here not in another IOPort
                                                   if (pos == this.Position) return true;
                                                   foreach (Thing t in this.Map.thingGrid.ThingsListAt(pos)) {
                                                       if (t is Building_StorageUnitIOPort) return false;
                                                   }
                                                   return true;
                                               });
                    }
                    else
                    {
                        GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), this.Position, this.Map, ThingPlaceMode.Near, null,
                                               // anywhere but on a storage unit io port.
                                               delegate (IntVec3 pos) {
                                                   foreach (Thing t in this.Map.thingGrid.ThingsListAt(pos)) {
                                                       if (t is Building_StorageUnitIOPort) return false;
                                                   }
                                                   return true;
                                               });
                    }
                }
                else
                {
                    GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), this.Position, this.Map, ThingPlaceMode.Near, null,
                                               // anywhere but on a storage unit io port.
                                               delegate (IntVec3 pos) {
                                                   foreach (Thing t in this.Map.thingGrid.ThingsListAt(pos)) {
                                                       if (t is Building_StorageUnitIOPort) return false;
                                                   }
                                                   return true;
                                               });
                }
            }
        }
    }

    public class DefModExtension_StorageUnitIOPortColor : DefModExtension
    {
        public Color inColor;
        public Color outColor;
    }
}

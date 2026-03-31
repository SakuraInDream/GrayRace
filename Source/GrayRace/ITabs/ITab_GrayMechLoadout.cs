using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class ITab_GrayMechLoadout : ITab
{
    private const float PanelWidth = 820f;
    private const float PanelHeight = 500f;
    private const float Margin = 10f;
    private const float CloseButtonReserveTop = 8f;
    private const float CloseButtonReserveRight = 44f;

    private readonly GrayMechSectionCanvasPanel sectionCanvasPanel = new();
    private readonly GrayMechLoadoutCanvasHost canvasHost;
    private readonly List<GRMechResolvedSlot> resolvedSlotScratch = new();
    private readonly List<GRMechResolvedSlot> weaponBuffer = new();
    private readonly List<GRMechResolvedSlot> supportBuffer = new();
    private readonly List<GRMechResolvedSlot> requiredBuffer = new();
    private readonly Dictionary<GRMechResolvedSlot, GRMechModuleDef> cachedModulesBySlot = new();
    private readonly Dictionary<GRMechResolvedSlot, string> cachedTooltipsBySlot = new();

    private CompGrayMechLoadout cachedLoadout;
    private int cachedSnapshotSignature = int.MinValue;

    internal List<GRMechResolvedSlot> WeaponSlots => weaponBuffer;

    internal List<GRMechResolvedSlot> SupportSlots => supportBuffer;

    internal List<GRMechResolvedSlot> RequiredSlots => requiredBuffer;

    public ITab_GrayMechLoadout()
    {
        canvasHost = new GrayMechLoadoutCanvasHost(this);
        size = new Vector2(PanelWidth, PanelHeight);
        labelKey = "配装";
    }

    private Pawn SelMech => SelThing as Pawn;

    private CompGrayMechLoadout SelLoadout => SelMech?.TryGetComp<CompGrayMechLoadout>();

    public override bool IsVisible => base.IsVisible && SelLoadout?.HasDesign == true;

    protected override void FillTab()
    {
        CompGrayMechLoadout loadout = SelLoadout;
        GrayMechDesignSnapshot snapshot = loadout?.DesignSnapshot;
        if (snapshot?.chassis == null)
        {
            return;
        }

        EnsureCache(loadout, snapshot);

        Rect root = new Rect(
            Margin,
            Margin + CloseButtonReserveTop,
            size.x - Margin * 2f - CloseButtonReserveRight,
            size.y - Margin * 2f - CloseButtonReserveTop);
        Widgets.DrawMenuSection(root);
        Widgets.DrawBoxSolid(root.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanelAlt);
        canvasHost.Bind(snapshot);
        sectionCanvasPanel.Draw(root.ContractedBy(8f), canvasHost);
    }

    private void EnsureCache(CompGrayMechLoadout loadout, GrayMechDesignSnapshot snapshot)
    {
        int signature = ComputeSnapshotSignature(snapshot);
        if (cachedLoadout == loadout && cachedSnapshotSignature == signature)
        {
            return;
        }

        cachedLoadout = loadout;
        cachedSnapshotSignature = signature;
        resolvedSlotScratch.Clear();
        weaponBuffer.Clear();
        supportBuffer.Clear();
        requiredBuffer.Clear();
        cachedModulesBySlot.Clear();
        cachedTooltipsBySlot.Clear();

        if (snapshot?.chassis == null)
        {
            return;
        }

        GrayMechDesignUtility.FillResolvedSlots(snapshot, resolvedSlotScratch);
        for (int i = 0; i < resolvedSlotScratch.Count; i++)
        {
            GRMechResolvedSlot resolvedSlot = resolvedSlotScratch[i];
            if (resolvedSlot?.slot == null)
            {
                continue;
            }

            GrayMechDesignUtility.TryGetSelectedModule(snapshot, resolvedSlot, out GRMechModuleDef module);
            cachedModulesBySlot[resolvedSlot] = module;
            cachedTooltipsBySlot[resolvedSlot] = GrayMechDrydockTabText.BuildSlotTooltip(snapshot.chassis, resolvedSlot, module);
        }
    }

    internal void PrepareSectionSlotsForCanvas(GRMechSectionSlotDef sectionSlot)
    {
        weaponBuffer.Clear();
        supportBuffer.Clear();

        for (int i = 0; i < resolvedSlotScratch.Count; i++)
        {
            GRMechResolvedSlot resolvedSlot = resolvedSlotScratch[i];
            if (resolvedSlot?.slot == null || !GRMechSectionSlotUtility.Matches(resolvedSlot.sectionSlot, sectionSlot))
            {
                continue;
            }

            switch (resolvedSlot.slot.slotCategory)
            {
                case GRMechSlotCategory.Weapon:
                    weaponBuffer.Add(resolvedSlot);
                    break;
                case GRMechSlotCategory.Utility:
                case GRMechSlotCategory.Auxiliary:
                    supportBuffer.Add(resolvedSlot);
                    break;
            }
        }
    }

    internal void PrepareCoreSlotsForCanvas()
    {
        requiredBuffer.Clear();

        for (int i = 0; i < resolvedSlotScratch.Count; i++)
        {
            GRMechResolvedSlot resolvedSlot = resolvedSlotScratch[i];
            if (resolvedSlot?.slot == null || resolvedSlot.sectionSlot != null)
            {
                continue;
            }

            requiredBuffer.Add(resolvedSlot);
        }
    }

    internal string GetSectionHeaderTextForCanvas(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot)
    {
        if (GrayMechDesignUtility.TryGetSelectedLayout(snapshot, sectionSlot, out GRMechSectionLayoutDef layout))
        {
            string layoutLabel = layout?.LabelCap.ToString();
            if (!layoutLabel.NullOrEmpty())
            {
                return layoutLabel;
            }
        }

        return sectionSlot?.LabelCap.ToString() ?? "Unknown";
    }

    internal void ResolveSlotVisualsForCanvas(GRMechResolvedSlot resolvedSlot, out GRMechModuleDef module, out string tooltip)
    {
        cachedModulesBySlot.TryGetValue(resolvedSlot, out module);
        cachedTooltipsBySlot.TryGetValue(resolvedSlot, out tooltip);
    }

    private static int ComputeSnapshotSignature(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.chassis == null)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + snapshot.chassis.GetHashCode();

            List<GrayMechSectionSelection> sections = snapshot.sections;
            if (sections != null)
            {
                hash = hash * 31 + sections.Count;
                for (int i = 0; i < sections.Count; i++)
                {
                    GrayMechSectionSelection section = sections[i];
                    hash = hash * 31 + (section?.sectionSlot?.GetHashCode() ?? 0);
                    hash = hash * 31 + (section?.layout?.GetHashCode() ?? 0);
                }
            }

            List<GrayMechModuleAssignment> modules = snapshot.modules;
            if (modules != null)
            {
                hash = hash * 31 + modules.Count;
                for (int i = 0; i < modules.Count; i++)
                {
                    GrayMechModuleAssignment module = modules[i];
                    hash = hash * 31 + (module?.sectionSlot?.GetHashCode() ?? 0);
                    hash = hash * 31 + (module?.slotKey?.GetHashCode() ?? 0);
                    hash = hash * 31 + (module?.module?.GetHashCode() ?? 0);
                }
            }

            return hash;
        }
    }

}

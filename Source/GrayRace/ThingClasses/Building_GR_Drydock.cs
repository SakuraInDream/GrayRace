using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.ThingClasses;

public class Building_GR_Drydock : Building, IThingHolder
{
    private GrayMechDesignSnapshot designDraft;
    private int editingDesignId = -1;
    private int designRevision;

    [Unsaved(false)]
    private CompGrayMechAssemblyBay cachedAssemblyBayComp;

    private WorldComponent_GrayMechDesignLibrary DesignLibrary => Find.World?.GetComponent<WorldComponent_GrayMechDesignLibrary>();

    private CompGrayMechAssemblyBay AssemblyBayComp => cachedAssemblyBayComp ??= this.TryGetComp<CompGrayMechAssemblyBay>();

    public GrayMechDesignSnapshot DesignDraft
    {
        get
        {
            EnsureDesignDraft();
            return designDraft;
        }
    }

    public int EditingDesignId => editingDesignId;

    public int DesignRevision => designRevision;

    public bool IsEditingSavedDesign => editingDesignId >= 0;

    public GrayMechDesignRecord EditingDesignRecord => editingDesignId >= 0 ? DesignLibrary?.GetDesign(editingDesignId) : null;

    public GrayMechAssemblyOrder CurrentOrder => AssemblyBayComp?.CurrentOrder;

    public int QueuedOrderCount => AssemblyBayComp?.QueuedOrderCount ?? 0;

    public int TotalQueuedOrderCount => AssemblyBayComp?.TotalQueuedOrderCount ?? 0;

    public bool HasActiveOrder => AssemblyBayComp?.HasActiveOrder ?? false;

    public List<GrayMechAssemblyOrder> QueuedOrders => AssemblyBayComp?.QueuedOrders;

    public int CurrentOrderTicksRemaining => AssemblyBayComp?.CurrentOrderTicksRemaining ?? 0;

    public int CurrentOrderTotalTicks => AssemblyBayComp?.CurrentOrderTotalTicks ?? 0;

    public float CurrentOrderProgressPercent => AssemblyBayComp?.CurrentOrderProgressPercent ?? 0f;

    public bool CurrentOrderNeedsMaterials => AssemblyBayComp?.CurrentOrderNeedsMaterials ?? false;

    public bool CanProgressNow => AssemblyBayComp?.CanProgressNow ?? false;

    public string CurrentOrderStatus => AssemblyBayComp?.CurrentOrderStatus ?? "Idle";

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref designDraft, "designDraft");
        Scribe_Values.Look(ref editingDesignId, "editingDesignId", -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            EnsureDesignDraft();
        }
    }

    public override string GetInspectString()
    {
        StringBuilder sb = new(base.GetInspectString());

        if (designDraft?.chassis != null)
        {
            sb.AppendInNewLine("Draft: " + designDraft.designLabel);
        }

        if (CurrentOrder != null)
        {
            sb.AppendInNewLine("Current build: " + CurrentOrder.Label);
            sb.AppendInNewLine("Status: " + CurrentOrderStatus);
            sb.AppendInNewLine("Progress: " + CurrentOrderProgressPercent.ToStringPercent());
            if (CurrentOrderTotalTicks > 0)
            {
                sb.AppendInNewLine("Time left: " + CurrentOrderTicksRemaining.ToStringTicksToPeriod());
            }

            AppendMaterialStatus(sb);
        }

        if (QueuedOrderCount > 0)
        {
            sb.AppendInNewLine("Queued builds: " + QueuedOrderCount);
        }

        return sb.ToString().TrimEndNewlines();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        if (!DebugSettings.ShowDevGizmos || CurrentOrder == null)
        {
            yield break;
        }

        yield return new Command_Action
        {
            action = CompleteCurrentOrder,
            defaultLabel = "DEV: Complete build"
        };
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return AssemblyBayComp?.GetDirectlyHeldThings();
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwner directThings = GetDirectlyHeldThings();
        if (directThings != null)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, directThings);
        }
    }

    public void LoadFromChassis(GRMechChassisDef chassis)
    {
        if (chassis == null)
        {
            return;
        }

        designDraft = GrayMechDesignUtility.CreateDefaultSnapshot(chassis);
        GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
        editingDesignId = -1;
        TouchDesignDraft();
    }

    public void LoadFromLibrary(GrayMechDesignRecord record)
    {
        if (record == null)
        {
            return;
        }

        designDraft = GrayMechDesignUtility.CloneSnapshot(record.snapshot);
        GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
        editingDesignId = record.id;
        if (designDraft != null)
        {
            designDraft.designLabel = record.label;
        }

        TouchDesignDraft();
    }

    public bool SetSectionLayout(GRMechSectionSlotDef sectionSlot, GRMechSectionLayoutDef layout)
    {
        EnsureDesignDraft();
        if (!GrayMechDesignUtility.SetSectionLayout(designDraft, sectionSlot, layout))
        {
            return false;
        }

        TouchDesignDraft();
        return true;
    }

    public bool SetModule(GRMechSectionSlotDef sectionSlot, string slotKey, GRMechModuleDef module)
    {
        EnsureDesignDraft();
        if (!GrayMechDesignUtility.SetModule(designDraft, sectionSlot, slotKey, module))
        {
            return false;
        }

        TouchDesignDraft();
        return true;
    }

    public GrayMechDesignRecord SaveAsNewDesign()
    {
        EnsureDesignDraft();
        WorldComponent_GrayMechDesignLibrary library = DesignLibrary;
        if (designDraft == null || library == null)
        {
            return null;
        }

        GrayMechDesignRecord record = library.CreateDesign(designDraft, designDraft.designLabel);
        editingDesignId = record.id;
        designDraft.designLabel = record.label;
        TouchDesignDraft();
        return record;
    }

    public bool SaveToCurrentDesign(out GrayMechDesignRecord record)
    {
        record = EditingDesignRecord;
        EnsureDesignDraft();
        if (designDraft == null || record == null)
        {
            return false;
        }

        if (!DesignLibrary.OverwriteDesign(record.id, designDraft))
        {
            return false;
        }

        record = EditingDesignRecord;
        designDraft.designLabel = record?.label ?? designDraft.designLabel;
        TouchDesignDraft();
        return true;
    }

    public bool DeleteCurrentDesign()
    {
        if (!IsEditingSavedDesign || DesignLibrary == null)
        {
            return false;
        }

        int deletedId = editingDesignId;
        editingDesignId = -1;
        bool deleted = DesignLibrary.DeleteDesign(deletedId);
        TouchDesignDraft();
        return deleted;
    }

    public void SyncDraftLabelFromSavedDesign()
    {
        GrayMechDesignRecord record = EditingDesignRecord;
        if (record != null && designDraft != null && designDraft.designLabel != record.label)
        {
            designDraft.designLabel = record.label;
            TouchDesignDraft();
        }
    }

    public bool TryQueueAssemblyOrder(out string reason)
    {
        if (AssemblyBayComp == null)
        {
            reason = "Assembly bay unavailable.";
            return false;
        }

        return AssemblyBayComp.TryQueueAssemblyOrder(DesignDraft, out reason);
    }

    public bool CanQueueAssemblyOrder(out string reason)
    {
        if (AssemblyBayComp == null)
        {
            reason = "Assembly bay unavailable.";
            return false;
        }

        return AssemblyBayComp.CanQueueAssemblyOrder(DesignDraft, out reason);
    }

    public bool CanAcceptIngredient(Thing thing)
    {
        return AssemblyBayComp?.CanAcceptIngredient(thing) ?? false;
    }

    public GrayMechAssemblyOrder GetQueuedOrder(int index)
    {
        return AssemblyBayComp?.GetQueuedOrder(index);
    }

    public bool TryCancelQueuedOrder(int index, out GrayMechAssemblyOrder removedOrder)
    {
        removedOrder = null;
        return AssemblyBayComp != null && AssemblyBayComp.TryCancelQueuedOrder(index, out removedOrder);
    }

    public bool TryCancelCurrentOrder(out GrayMechAssemblyOrder removedOrder)
    {
        removedOrder = null;
        return AssemblyBayComp != null && AssemblyBayComp.TryCancelCurrentOrder(out removedOrder);
    }

    public bool CanBuildChassis(GRMechChassisDef chassis, out string reason)
    {
        if (AssemblyBayComp == null)
        {
            reason = "Assembly bay unavailable.";
            return false;
        }

        return AssemblyBayComp.CanBuildChassis(chassis, out reason);
    }

    public int GetRequiredCountOf(ThingDef thingDef)
    {
        return AssemblyBayComp?.GetRequiredCountOf(thingDef) ?? 0;
    }

    public int GetLoadedCountOf(ThingDef thingDef)
    {
        return AssemblyBayComp?.GetLoadedCountOf(thingDef) ?? 0;
    }

    public void AppendMaterialStatus(StringBuilder sb)
    {
        AssemblyBayComp?.AppendMaterialStatus(sb);
    }

    public static bool WasLoadingCancelled(Thing thing)
    {
        return CompGrayMechAssemblyBay.WasLoadingCancelled(thing);
    }

    private void EnsureDesignDraft()
    {
        if (designDraft != null)
        {
            GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
            return;
        }

        GrayMechDesignRecord savedDesign = EditingDesignRecord;
        if (savedDesign?.snapshot != null)
        {
            designDraft = GrayMechDesignUtility.CloneSnapshot(savedDesign.snapshot);
            designDraft.designLabel = savedDesign.label;
            GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
            return;
        }

        GRMechChassisDef firstChassis = FindFirstChassis();
        if (firstChassis != null)
        {
            designDraft = GrayMechDesignUtility.CreateDefaultSnapshot(firstChassis);
            GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
        }
    }

    private void CompleteCurrentOrder()
    {
        AssemblyBayComp?.DevCompleteCurrentOrder();
    }

    private void TouchDesignDraft()
    {
        designRevision++;
    }

    private static GRMechChassisDef FindFirstChassis()
    {
        List<GRMechChassisDef> chassisDefs = DefDatabase<GRMechChassisDef>.AllDefsListForReading;
        GRMechChassisDef best = null;
        for (int i = 0; i < chassisDefs.Count; i++)
        {
            GRMechChassisDef current = chassisDefs[i];
            if (current == null || (best != null && CompareChassisDefs(current, best) >= 0))
            {
                continue;
            }

            best = current;
        }

        return best;
    }

    private static int CompareChassisDefs(GRMechChassisDef left, GRMechChassisDef right)
    {
        int order = left.uiOrder.CompareTo(right.uiOrder);
        if (order != 0)
        {
            return order;
        }

        return string.CompareOrdinal(left.label, right.label);
    }
}

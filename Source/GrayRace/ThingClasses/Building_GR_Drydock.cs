using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Bills;
using SD.GrayRace.DefModExtensions;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.ThingClasses;

public class Building_GR_Drydock : Building_WorkTableAutonomous
{
    private GrayMechDesignSnapshot designDraft;
    private int editingDesignId = -1;
    private string sourcePresetDefName;
    private int designRevision;

    private static readonly List<RecipeDef> tmpMatchingRecipes = new();

    private WorldComponent_GrayMechDesignLibrary DesignLibrary => Find.World?.GetComponent<WorldComponent_GrayMechDesignLibrary>();

    public GrayMechDesignSnapshot DesignDraft
    {
        get
        {
            EnsureDesignDraft();
            return designDraft;
        }
    }

    public int EditingDesignId => editingDesignId;

    public string SourcePresetDefName => sourcePresetDefName;

    public int DesignRevision => designRevision;

    public bool IsEditingSavedDesign => editingDesignId >= 0;

    public GrayMechDesignRecord EditingDesignRecord => editingDesignId >= 0 ? DesignLibrary?.GetDesign(editingDesignId) : null;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref designDraft, "designDraft");
        Scribe_Values.Look(ref editingDesignId, "editingDesignId", -1);
        Scribe_Values.Look(ref sourcePresetDefName, "sourcePresetDefName");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            EnsureDesignDraft();
        }
    }

    protected override string GetInspectStringExtra()
    {
        EnsureDesignDraft();

        if (ActiveBill is Bill_GrayMechAssembly bill)
        {
            return "Active design: " + bill.LabelCap;
        }

        if (designDraft?.chassis != null)
        {
            return "Draft: " + designDraft.designLabel;
        }

        return null;
    }

    public override void Notify_FormingCompleted()
    {
        Thing thing = activeBill?.CreateProducts();
        innerContainer.ClearAndDestroyContents();
        if (thing != null)
        {
            innerContainer.TryAdd(thing);
            Messages.Message("Assembly complete: " + thing.LabelCap, this, MessageTypeDefOf.PositiveEvent);
        }
    }

    public void LoadFromPreset(GRMechPresetDef preset)
    {
        if (preset == null)
        {
            return;
        }

        designDraft = GrayMechDesignUtility.CreateSnapshot(preset);
        GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
        editingDesignId = -1;
        sourcePresetDefName = preset.defName;
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
        sourcePresetDefName = record.sourcePresetDefName;
        if (designDraft != null)
        {
            designDraft.designLabel = record.label;
        }

        TouchDesignDraft();
    }

    public bool SetSectionLayout(GRMechSectionRoleDef role, GRMechSectionLayoutDef layout)
    {
        EnsureDesignDraft();
        if (!GrayMechDesignUtility.SetSectionLayout(designDraft, role, layout))
        {
            return false;
        }

        TouchDesignDraft();
        return true;
    }

    public bool SetModule(string slotKey, GRMechModuleDef module)
    {
        EnsureDesignDraft();
        if (!GrayMechDesignUtility.SetModule(designDraft, slotKey, module))
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

        GrayMechDesignRecord record = library.CreateDesign(designDraft, designDraft.designLabel, sourcePresetDefName);
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

        if (!DesignLibrary.OverwriteDesign(record.id, designDraft, sourcePresetDefName))
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

    public bool TryQueueAssemblyBill(out string reason)
    {
        if (!CanQueueAssemblyBill(out RecipeDef recipe, out reason))
        {
            return false;
        }

        Bill bill = recipe.MakeNewBill();
        if (bill is not Bill_GrayMechAssembly assemblyBill)
        {
            reason = "Recipe did not create a Gray mech assembly bill.";
            return false;
        }

        assemblyBill.SetDesignSnapshot(designDraft);
        billStack.AddBill(assemblyBill);
        return true;
    }

    public bool CanQueueAssemblyBill(out string reason)
    {
        return CanQueueAssemblyBill(out _, out reason);
    }

    public GRMechPresetDef GetSourcePreset()
    {
        return sourcePresetDefName.NullOrEmpty() ? null : DefDatabase<GRMechPresetDef>.GetNamedSilentFail(sourcePresetDefName);
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

        GRMechPresetDef preset = GetSourcePreset();
        if (preset != null)
        {
            designDraft = GrayMechDesignUtility.CreateSnapshot(preset);
            GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
            return;
        }

        GRMechPresetDef firstPreset = FindFirstPreset();
        if (firstPreset != null)
        {
            LoadFromPreset(firstPreset);
            return;
        }

        GRMechChassisDef firstChassis = FindFirstChassis();
        if (firstChassis != null)
        {
            designDraft = GrayMechDesignUtility.CreateDefaultSnapshot(firstChassis);
            GrayMechDesignUtility.EnsureSnapshotDefaults(designDraft);
        }
    }

    private RecipeDef FindAssemblyRecipeForDraft()
    {
        tmpMatchingRecipes.Clear();
        List<RecipeDef> recipes = def.AllRecipes;
        for (int i = 0; i < recipes.Count; i++)
        {
            RecipeDef recipe = recipes[i];
            if (recipe == null)
            {
                continue;
            }

            DefModExtension_MechAssemblyRecipe extension = recipe.GetModExtension<DefModExtension_MechAssemblyRecipe>();
            if (extension?.preset?.chassis == designDraft?.chassis)
            {
                tmpMatchingRecipes.Add(recipe);
            }
        }

        if (tmpMatchingRecipes.Count == 0)
        {
            return null;
        }

        tmpMatchingRecipes.SortBy(r => r.displayPriority, r => r.label);
        return tmpMatchingRecipes[0];
    }

    private bool CanQueueAssemblyBill(out RecipeDef recipe, out string reason)
    {
        reason = string.Empty;
        recipe = null;
        EnsureDesignDraft();

        if (billStack.Count >= BillStack.MaxCount)
        {
            reason = "Bill limit reached.";
            return false;
        }

        if (designDraft?.chassis?.ProducedRace == null)
        {
            reason = "Current draft is incomplete.";
            return false;
        }

        if (GrayMechDesignUtility.TryGetFirstMissingResearch(designDraft, out ResearchProjectDef missingProject))
        {
            reason = "Missing research: " + missingProject.LabelCap;
            return false;
        }

        recipe = FindAssemblyRecipeForDraft();
        if (recipe == null)
        {
            reason = "No compatible assembly recipe found.";
            return false;
        }

        if (!recipe.AvailableNow || !recipe.AvailableOnNow(this))
        {
            reason = "Assembly recipe is not currently available.";
            return false;
        }

        if (ModsConfig.BiotechActive && recipe.mechanitorOnlyRecipe && !AnyMechanitorAvailable())
        {
            reason = "A mechanitor is required to queue this design.";
            return false;
        }

        return true;
    }

    private void TouchDesignDraft()
    {
        designRevision++;
    }

    private bool AnyMechanitorAvailable()
    {
        if (Map?.mapPawns?.FreeColonists == null)
        {
            return false;
        }

        List<Pawn> colonists = Map.mapPawns.FreeColonists;
        for (int i = 0; i < colonists.Count; i++)
        {
            if (MechanitorUtility.IsMechanitor(colonists[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static GRMechPresetDef FindFirstPreset()
    {
        List<GRMechPresetDef> presets = DefDatabase<GRMechPresetDef>.AllDefsListForReading;
        GRMechPresetDef best = null;
        for (int i = 0; i < presets.Count; i++)
        {
            GRMechPresetDef current = presets[i];
            if (current == null || (best != null && ComparePresetDefs(current, best) >= 0))
            {
                continue;
            }

            best = current;
        }

        return best;
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

    private static int ComparePresetDefs(GRMechPresetDef left, GRMechPresetDef right)
    {
        int order = left.uiOrder.CompareTo(right.uiOrder);
        if (order != 0)
        {
            return order;
        }

        return string.CompareOrdinal(left.label, right.label);
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

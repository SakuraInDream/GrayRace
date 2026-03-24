using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.DefModExtensions;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.ThingClasses;

public class Building_GR_Drydock : Building, IThingHolder
{
    private const int MaxQueueCount = 16;

    public ThingOwner innerContainer;

    private GrayMechDesignSnapshot designDraft;
    private int editingDesignId = -1;
    private int designRevision;
    private GrayMechAssemblyOrder currentOrder;
    private List<GrayMechAssemblyOrder> queuedOrders = new();

    [Unsaved(false)]
    private CompPowerTrader cachedPowerComp;

    [Unsaved(false)]
    private CompBreakdownable cachedBreakdownableComp;

    private static readonly List<RecipeDef> tmpMatchingRecipes = new();
    private static readonly List<IngredientCount> tmpRequiredIngredients = new();
    private static readonly List<ThingDefCountClass> tmpRequiredCosts = new();

    private WorldComponent_GrayMechDesignLibrary DesignLibrary => Find.World?.GetComponent<WorldComponent_GrayMechDesignLibrary>();

    private CompPowerTrader PowerTraderComp => cachedPowerComp ??= this.TryGetComp<CompPowerTrader>();

    private CompBreakdownable BreakdownableComp => cachedBreakdownableComp ??= this.TryGetComp<CompBreakdownable>();

    public Building_GR_Drydock()
    {
        innerContainer = new ThingOwner<Thing>(this);
    }

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

    public GrayMechAssemblyOrder CurrentOrder => currentOrder;

    public int QueuedOrderCount => queuedOrders?.Count ?? 0;

    public int TotalQueuedOrderCount => QueuedOrderCount + (currentOrder != null ? 1 : 0);

    public bool HasActiveOrder => currentOrder != null;

    public int CurrentOrderTicksRemaining => currentOrder?.ticksRemaining ?? 0;

    public int CurrentOrderTotalTicks => currentOrder?.totalTicks ?? 0;

    public float CurrentOrderProgressPercent => currentOrder?.ProgressPercent ?? 0f;

    public bool CurrentOrderNeedsMaterials => currentOrder != null && !HasAllRequiredMaterials(currentOrder);

    public bool CanProgressNow
    {
        get
        {
            if (currentOrder == null || CurrentOrderNeedsMaterials)
            {
                return false;
            }

            if (PowerTraderComp != null && !PowerTraderComp.PowerOn)
            {
                return false;
            }

            return BreakdownableComp == null || !BreakdownableComp.BrokenDown;
        }
    }

    public string CurrentOrderStatus
    {
        get
        {
            if (currentOrder == null)
            {
                return QueuedOrderCount > 0 ? "Queued" : "Idle";
            }

            if (CurrentOrderNeedsMaterials)
            {
                return "Waiting for materials";
            }

            if (PowerTraderComp != null && !PowerTraderComp.PowerOn)
            {
                return "Paused: No power";
            }

            if (BreakdownableComp != null && BreakdownableComp.BrokenDown)
            {
                return "Paused: Broken down";
            }

            return "Building";
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        Scribe_Deep.Look(ref designDraft, "designDraft");
        Scribe_Values.Look(ref editingDesignId, "editingDesignId", -1);
        Scribe_Deep.Look(ref currentOrder, "currentOrder");
        Scribe_Collections.Look(ref queuedOrders, "queuedOrders", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            innerContainer ??= new ThingOwner<Thing>(this);
            queuedOrders ??= new List<GrayMechAssemblyOrder>();
            EnsureDesignDraft();
            FixupOrder(currentOrder);
            for (int i = 0; i < queuedOrders.Count; i++)
            {
                FixupOrder(queuedOrders[i]);
            }

            TryStartNextQueuedOrder();
        }
    }

    protected override void Tick()
    {
        base.Tick();

        if (this.IsHashIntervalTick(250) && PowerTraderComp != null)
        {
            bool active = currentOrder != null && CanProgressNow;
            PowerTraderComp.PowerOutput = active ? 0f - PowerTraderComp.Props.PowerConsumption : 0f - PowerTraderComp.Props.idlePowerDraw;
        }

        if (currentOrder == null)
        {
            TryStartNextQueuedOrder();
            return;
        }

        if (!CanProgressNow)
        {
            return;
        }

        currentOrder.ticksRemaining--;
        if (currentOrder.ticksRemaining <= 0)
        {
            CompleteCurrentOrder();
        }
    }

    public override string GetInspectString()
    {
        StringBuilder sb = new(base.GetInspectString());

        if (designDraft?.chassis != null)
        {
            sb.AppendInNewLine("Draft: " + designDraft.designLabel);
        }

        if (currentOrder != null)
        {
            sb.AppendInNewLine("Current build: " + currentOrder.Label);
            sb.AppendInNewLine("Status: " + CurrentOrderStatus);
            sb.AppendInNewLine("Progress: " + CurrentOrderProgressPercent.ToStringPercent());
            if (CurrentOrderTotalTicks > 0)
            {
                sb.AppendInNewLine("Time left: " + CurrentOrderTicksRemaining.ToStringTicksToPeriod());
            }

            AppendMaterialStatus(sb, currentOrder);
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

        if (!DebugSettings.ShowDevGizmos || currentOrder == null)
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
        return innerContainer;
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
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
        if (!TryCreateAssemblyOrder(out GrayMechAssemblyOrder order, out reason))
        {
            return false;
        }

        if (currentOrder == null)
        {
            currentOrder = order;
        }
        else
        {
            queuedOrders.Add(order);
        }

        return true;
    }

    public bool CanQueueAssemblyOrder(out string reason)
    {
        return TryCreateAssemblyOrder(out _, out reason);
    }

    public bool CanAcceptIngredient(Thing thing)
    {
        if (thing == null || currentOrder == null)
        {
            return false;
        }

        int missingCount = GetMissingCountForCurrentOrder(thing.def);
        return missingCount > 0;
    }

    public int GetRequiredCountOf(ThingDef thingDef)
    {
        if (thingDef == null || currentOrder == null)
        {
            return 0;
        }

        BuildIngredientBuffers(currentOrder, tmpRequiredIngredients, tmpRequiredCosts);
        for (int i = 0; i < tmpRequiredIngredients.Count; i++)
        {
            IngredientCount ingredient = tmpRequiredIngredients[i];
            if (ingredient?.FixedIngredient == thingDef)
            {
                return ingredient.CountRequiredOfFor(thingDef, currentOrder.recipe, null);
            }
        }

        return 0;
    }

    public int GetLoadedCountOf(ThingDef thingDef)
    {
        return thingDef == null ? 0 : innerContainer.TotalStackCountOfDef(thingDef);
    }

    public static bool WasLoadingCancelled(Thing thing)
    {
        return thing is not Building_GR_Drydock drydock || !drydock.HasActiveOrder || !drydock.CurrentOrderNeedsMaterials;
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

    private RecipeDef FindAssemblyRecipeForDraft()
    {
        tmpMatchingRecipes.Clear();
        List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
        for (int i = 0; i < recipes.Count; i++)
        {
            RecipeDef recipe = recipes[i];
            if (recipe == null)
            {
                continue;
            }

            DefModExtension_MechAssemblyRecipe extension = recipe.GetModExtension<DefModExtension_MechAssemblyRecipe>();
            if (extension?.chassis == designDraft?.chassis)
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

    private bool TryCreateAssemblyOrder(out GrayMechAssemblyOrder order, out string reason)
    {
        order = null;
        reason = string.Empty;
        EnsureDesignDraft();

        if (TotalQueuedOrderCount >= MaxQueueCount)
        {
            reason = "Build queue is full.";
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

        RecipeDef recipe = FindAssemblyRecipeForDraft();
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

        if (!TryFindAssignedMechanitor(recipe, out Pawn mechanitor, out reason))
        {
            return false;
        }

        int totalTicks = GetBuildTicks(designDraft, recipe);
        order = new GrayMechAssemblyOrder
        {
            designSnapshot = GrayMechDesignUtility.CloneSnapshot(designDraft),
            recipe = recipe,
            assignedMechanitor = mechanitor,
            totalTicks = totalTicks,
            ticksRemaining = totalTicks
        };
        return true;
    }

    private bool TryFindAssignedMechanitor(RecipeDef recipe, out Pawn mechanitor, out string reason)
    {
        mechanitor = null;
        reason = string.Empty;

        if (Map?.mapPawns?.FreeColonists == null)
        {
            reason = "No colonist available.";
            return false;
        }

        List<Pawn> colonists = Map.mapPawns.FreeColonists;
        Pawn fallback = null;
        float bandwidthCost = GetBandwidthCost(designDraft?.chassis?.ProducedRace);
        for (int i = 0; i < colonists.Count; i++)
        {
            Pawn colonist = colonists[i];
            if (colonist == null || colonist.Dead || colonist.Downed || colonist.MapHeld != Map)
            {
                continue;
            }

            fallback ??= colonist;
            if (!recipe.mechanitorOnlyRecipe)
            {
                mechanitor = fallback;
                return true;
            }

            if (!MechanitorUtility.IsMechanitor(colonist))
            {
                continue;
            }

            if (!HasAvailableQueuedBandwidth(colonist, bandwidthCost))
            {
                continue;
            }

            mechanitor = colonist;
            return true;
        }

        if (!recipe.mechanitorOnlyRecipe)
        {
            if (fallback != null)
            {
                mechanitor = fallback;
                return true;
            }

            reason = "No colonist available.";
            return false;
        }

        reason = "No mechanitor has enough available bandwidth.";
        return false;
    }

    private void TryStartNextQueuedOrder()
    {
        if (currentOrder != null || queuedOrders == null || queuedOrders.Count == 0)
        {
            return;
        }

        currentOrder = queuedOrders[0];
        queuedOrders.RemoveAt(0);
        FixupOrder(currentOrder);
    }

    private void CompleteCurrentOrder()
    {
        GrayMechAssemblyOrder completedOrder = currentOrder;
        currentOrder = null;
        if (completedOrder?.designSnapshot?.chassis?.pawnKindDef == null || Map == null)
        {
            TryStartNextQueuedOrder();
            return;
        }

        ConsumeCurrentOrderIngredients(completedOrder);

        Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            completedOrder.designSnapshot.chassis.pawnKindDef,
            completedOrder.assignedMechanitor?.Faction ?? Faction.OfPlayer,
            PawnGenerationContext.NonPlayer,
            null,
            forceGenerateNewPawn: false,
            allowDead: false,
            allowDowned: true,
            canGeneratePawnRelations: true,
            mustBeCapableOfViolence: false,
            1f,
            forceAddFreeWarmLayerIfNeeded: false,
            allowGay: true,
            allowPregnant: false,
            allowFood: true,
            allowAddictions: true,
            inhabitant: false,
            certainlyBeenInCryptosleep: false,
            forceRedressWorldPawnIfFormerColonist: false,
            worldPawnFactionDoesntMatter: false,
            0f,
            0f,
            null,
            1f,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            forceNoIdeo: false,
            forceNoBackstory: false,
            forbidAnyTitle: false,
            forceDead: false,
            null,
            null,
            null,
            null,
            null,
            0f,
            DevelopmentalStage.Newborn));

        if (completedOrder.assignedMechanitor != null && !completedOrder.assignedMechanitor.Dead)
        {
            completedOrder.assignedMechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
        }

        if (!GenPlace.TryPlaceThing(mech, InteractionCell, Map, ThingPlaceMode.Near, out Thing resultingThing))
        {
            GenPlace.TryPlaceThing(mech, Position, Map, ThingPlaceMode.Near, out resultingThing);
        }

        if (resultingThing is Pawn mechPawn)
        {
            mechPawn.TryGetComp<CompGrayMechLoadout>()?.ApplyDesign(completedOrder.designSnapshot);
        }

        Messages.Message("Assembly complete: " + (resultingThing ?? mech).LabelCap, this, MessageTypeDefOf.PositiveEvent);
        TryStartNextQueuedOrder();
    }

    private bool HasAllRequiredMaterials(GrayMechAssemblyOrder order)
    {
        if (order == null)
        {
            return false;
        }

        BuildIngredientBuffers(order, tmpRequiredIngredients, tmpRequiredCosts);
        for (int i = 0; i < tmpRequiredIngredients.Count; i++)
        {
            IngredientCount ingredient = tmpRequiredIngredients[i];
            ThingDef thingDef = ingredient?.FixedIngredient;
            if (thingDef == null)
            {
                continue;
            }

            int required = ingredient.CountRequiredOfFor(thingDef, order.recipe, null);
            if (GetLoadedCountOf(thingDef) < required)
            {
                return false;
            }
        }

        return true;
    }

    private int GetMissingCountForCurrentOrder(ThingDef thingDef)
    {
        if (thingDef == null || currentOrder == null)
        {
            return 0;
        }

        int required = GetRequiredCountOf(thingDef);
        int loaded = GetLoadedCountOf(thingDef);
        int missing = required - loaded;
        return missing > 0 ? missing : 0;
    }

    private void ConsumeCurrentOrderIngredients(GrayMechAssemblyOrder order)
    {
        BuildIngredientBuffers(order, tmpRequiredIngredients, tmpRequiredCosts);
        for (int i = 0; i < tmpRequiredIngredients.Count; i++)
        {
            IngredientCount ingredient = tmpRequiredIngredients[i];
            ThingDef thingDef = ingredient?.FixedIngredient;
            if (thingDef == null)
            {
                continue;
            }

            int remaining = ingredient.CountRequiredOfFor(thingDef, order.recipe, null);
            for (int j = innerContainer.Count - 1; j >= 0 && remaining > 0; j--)
            {
                Thing storedThing = innerContainer[j];
                if (storedThing.def != thingDef)
                {
                    continue;
                }

                int consumeCount = remaining < storedThing.stackCount ? remaining : storedThing.stackCount;
                Thing consumedThing = consumeCount >= storedThing.stackCount ? storedThing : storedThing.SplitOff(consumeCount);
                remaining -= consumeCount;
                order.recipe.Worker.ConsumeIngredient(consumedThing, order.recipe, Map);
            }
        }
    }

    private static void BuildIngredientBuffers(GrayMechAssemblyOrder order, List<IngredientCount> ingredientBuffer, List<ThingDefCountClass> costBuffer)
    {
        GrayMechDesignUtility.BuildIngredientList(order?.designSnapshot, ingredientBuffer, costBuffer);
    }

    private void AppendMaterialStatus(StringBuilder sb, GrayMechAssemblyOrder order)
    {
        BuildIngredientBuffers(order, tmpRequiredIngredients, tmpRequiredCosts);
        if (tmpRequiredIngredients.Count == 0)
        {
            return;
        }

        sb.AppendInNewLine("Materials:");
        for (int i = 0; i < tmpRequiredIngredients.Count; i++)
        {
            IngredientCount ingredient = tmpRequiredIngredients[i];
            ThingDef thingDef = ingredient?.FixedIngredient;
            if (thingDef == null)
            {
                continue;
            }

            int required = ingredient.CountRequiredOfFor(thingDef, order.recipe, null);
            int loaded = GetLoadedCountOf(thingDef);
            sb.AppendInNewLine("  " + thingDef.LabelCap + " " + loaded + " / " + required);
        }
    }

    private static int GetBuildTicks(GrayMechDesignSnapshot snapshot, RecipeDef recipe)
    {
        int chassisTicks = snapshot?.chassis?.fixedWorkTicks ?? 0;
        if (chassisTicks > 0)
        {
            return chassisTicks;
        }

        int gestationCycles = recipe?.gestationCycles ?? 1;
        if (gestationCycles < 1)
        {
            gestationCycles = 1;
        }

        int formingTicks = recipe?.formingTicks ?? 0;
        int totalTicks = formingTicks * gestationCycles;
        return totalTicks > 0 ? totalTicks : 60000;
    }

    private static float GetBandwidthCost(ThingDef producedRace)
    {
        if (producedRace != null)
        {
            return producedRace.GetStatValueAbstract(StatDefOf.BandwidthCost);
        }

        return 0f;
    }

    private bool HasAvailableQueuedBandwidth(Pawn mechanitor, float bandwidthCost)
    {
        if (bandwidthCost <= 0f)
        {
            return true;
        }

        if (mechanitor?.mechanitor == null)
        {
            return false;
        }

        float usedBandwidth = mechanitor.mechanitor.UsedBandwidthFromSubjects + GetReservedQueuedBandwidth(mechanitor);
        return usedBandwidth + bandwidthCost <= mechanitor.mechanitor.TotalBandwidth;
    }

    private float GetReservedQueuedBandwidth(Pawn mechanitor)
    {
        float total = 0f;
        if (mechanitor == null)
        {
            return total;
        }

        if (currentOrder?.assignedMechanitor == mechanitor)
        {
            total += GetBandwidthCost(currentOrder.ProducedRace);
        }

        if (queuedOrders != null)
        {
            for (int i = 0; i < queuedOrders.Count; i++)
            {
                GrayMechAssemblyOrder order = queuedOrders[i];
                if (order?.assignedMechanitor == mechanitor)
                {
                    total += GetBandwidthCost(order.ProducedRace);
                }
            }
        }

        return total;
    }

    private void FixupOrder(GrayMechAssemblyOrder order)
    {
        if (order == null)
        {
            return;
        }

        if (order.totalTicks <= 0)
        {
            order.totalTicks = GetBuildTicks(order.designSnapshot, order.recipe);
        }

        if (order.ticksRemaining <= 0)
        {
            order.ticksRemaining = order.totalTicks;
        }
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

using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.DefModExtensions;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.Comps;

public class CompGrayMechAssemblyBay : ThingComp, IThingHolder
{
    private const int MaxQueueCount = 16;

    private ThingOwner innerContainer;
    private GrayMechAssemblyOrder currentOrder;
    private List<GrayMechAssemblyOrder> queuedOrders = new();

    [Unsaved]
    private CompPowerTrader cachedPowerComp;

    [Unsaved]
    private CompBreakdownable cachedBreakdownableComp;

    private static readonly List<RecipeDef> tmpMatchingRecipes = new();
    private static readonly List<IngredientCount> tmpRequiredIngredients = new();
    private static readonly List<ThingDefCountClass> tmpRequiredCosts = new();

    private CompPropertiesGrayMechAssemblyBay Props => (CompPropertiesGrayMechAssemblyBay)props;

    private CompPowerTrader PowerTraderComp => cachedPowerComp ??= parent.TryGetComp<CompPowerTrader>();

    private CompBreakdownable BreakdownableComp => cachedBreakdownableComp ??= parent.TryGetComp<CompBreakdownable>();

    public GrayMechAssemblyOrder CurrentOrder => currentOrder;

    public int QueuedOrderCount => queuedOrders?.Count ?? 0;

    public int TotalQueuedOrderCount => QueuedOrderCount + (currentOrder != null ? 1 : 0);

    public bool HasActiveOrder => currentOrder != null;

    public List<GrayMechAssemblyOrder> QueuedOrders => queuedOrders ??= new List<GrayMechAssemblyOrder>();

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

    public override void PostPostMake()
    {
        base.PostPostMake();
        innerContainer ??= new ThingOwner<Thing>(this);
        queuedOrders ??= new List<GrayMechAssemblyOrder>();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        Scribe_Deep.Look(ref currentOrder, "currentOrder");
        Scribe_Collections.Look(ref queuedOrders, "queuedOrders", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            innerContainer ??= new ThingOwner<Thing>(this);
            queuedOrders ??= new List<GrayMechAssemblyOrder>();
            FixupOrder(currentOrder);
            for (int i = 0; i < queuedOrders.Count; i++)
            {
                FixupOrder(queuedOrders[i]);
            }

            TryStartNextQueuedOrder();
        }
    }

    public override void CompTick()
    {
        if (parent.IsHashIntervalTick(250) && PowerTraderComp != null)
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

    public bool TryQueueAssemblyOrder(GrayMechDesignSnapshot designSnapshot, out string reason)
    {
        if (!TryCreateAssemblyOrder(designSnapshot, out GrayMechAssemblyOrder order, out reason))
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

    public bool CanQueueAssemblyOrder(GrayMechDesignSnapshot designSnapshot, out string reason)
    {
        return TryCreateAssemblyOrder(designSnapshot, out _, out reason);
    }

    public GrayMechAssemblyOrder GetQueuedOrder(int index)
    {
        if (queuedOrders == null || index < 0 || index >= queuedOrders.Count)
        {
            return null;
        }

        return queuedOrders[index];
    }

    public bool TryCancelQueuedOrder(int index, out GrayMechAssemblyOrder removedOrder)
    {
        removedOrder = null;
        if (queuedOrders == null || index < 0 || index >= queuedOrders.Count)
        {
            return false;
        }

        removedOrder = queuedOrders[index];
        queuedOrders.RemoveAt(index);
        if (currentOrder == null)
        {
            TryStartNextQueuedOrder();
        }

        return true;
    }

    public bool TryCancelCurrentOrder(out GrayMechAssemblyOrder removedOrder)
    {
        removedOrder = currentOrder;
        if (removedOrder == null)
        {
            return false;
        }

        currentOrder = null;
        EjectStoredIngredients();
        TryStartNextQueuedOrder();
        return true;
    }

    public bool CanBuildChassis(GRMechChassisDef chassis, out string reason)
    {
        reason = string.Empty;
        if (chassis == null)
        {
            reason = "No chassis selected.";
            return false;
        }

        List<GRMechChassisDef> allowedChassis = Props?.allowedChassis;
        if (allowedChassis != null && allowedChassis.Count > 0 && !allowedChassis.Contains(chassis))
        {
            reason = "This assembly bay cannot build " + chassis.LabelCap + ".";
            return false;
        }

        List<GRMechChassisDef> blockedChassis = Props?.blockedChassis;
        if (blockedChassis != null && blockedChassis.Contains(chassis))
        {
            reason = "This assembly bay cannot build " + chassis.LabelCap + ".";
            return false;
        }

        return true;
    }

    public bool CanAcceptIngredient(Thing thing)
    {
        if (thing == null || currentOrder == null)
        {
            return false;
        }

        return GetMissingCountForCurrentOrder(thing.def) > 0;
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
        return thingDef == null || innerContainer == null ? 0 : innerContainer.TotalStackCountOfDef(thingDef);
    }

    public void AppendMaterialStatus(StringBuilder sb)
    {
        if (sb == null || currentOrder == null)
        {
            return;
        }

        AppendMaterialStatus(sb, currentOrder);
    }

    public void DevCompleteCurrentOrder()
    {
        if (currentOrder != null)
        {
            CompleteCurrentOrder();
        }
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return innerContainer;
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        if (innerContainer != null)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }
    }

    public static bool WasLoadingCancelled(Thing thing)
    {
        CompGrayMechAssemblyBay assemblyBay = thing?.TryGetComp<CompGrayMechAssemblyBay>();
        return assemblyBay == null || !assemblyBay.HasActiveOrder || !assemblyBay.CurrentOrderNeedsMaterials;
    }

    private void EjectStoredIngredients()
    {
        if (innerContainer == null || innerContainer.Count == 0)
        {
            return;
        }

        Map map = parent.MapHeld;
        if (map == null)
        {
            return;
        }

        IntVec3 dropCell = parent.InteractionCell;
        if (!dropCell.IsValid)
        {
            dropCell = parent.PositionHeld;
        }

        innerContainer.TryDropAll(dropCell, map, ThingPlaceMode.Near);
    }

    private bool TryCreateAssemblyOrder(GrayMechDesignSnapshot designSnapshot, out GrayMechAssemblyOrder order, out string reason)
    {
        order = null;
        reason = string.Empty;
        GrayMechDesignUtility.EnsureSnapshotDefaults(designSnapshot);

        if (TotalQueuedOrderCount >= MaxQueueCount)
        {
            reason = "Build queue is full.";
            return false;
        }

        if (designSnapshot?.chassis?.ProducedRace == null)
        {
            reason = "Current draft is incomplete.";
            return false;
        }

        if (!CanBuildChassis(designSnapshot.chassis, out reason))
        {
            return false;
        }

        if (GrayMechDesignUtility.TryGetFirstMissingRequiredModule(designSnapshot, out GRMechSlotEntry missingSlot))
        {
            reason = "Missing required core system: " + (missingSlot.label.NullOrEmpty() ? missingSlot.key : missingSlot.label);
            return false;
        }

        if (!GrayMechDesignUtility.TryResolvePrimaryEquipmentModule(designSnapshot, out _, out reason))
        {
            return false;
        }

        if (GrayMechDesignUtility.TryGetFirstMissingResearch(designSnapshot, out ResearchProjectDef missingProject))
        {
            reason = "Missing research: " + missingProject.LabelCap;
            return false;
        }

        if (GrayMechDesignUtility.TryGetPowerDeficit(designSnapshot, out int powerDeficit))
        {
            reason = "Insufficient reactor output. Power deficit: " + powerDeficit + ".";
            return false;
        }

        RecipeDef recipe = FindAssemblyRecipeForSnapshot(designSnapshot);
        if (recipe == null)
        {
            reason = "No compatible assembly recipe found.";
            return false;
        }

        if (!recipe.AvailableNow || !recipe.AvailableOnNow(parent))
        {
            reason = "Assembly recipe is not currently available.";
            return false;
        }

        if (!TryFindAssignedMechanitor(designSnapshot, recipe, out Pawn mechanitor, out reason))
        {
            return false;
        }

        int totalTicks = GetBuildTicks(designSnapshot, recipe);
        order = new GrayMechAssemblyOrder
        {
            designSnapshot = GrayMechDesignUtility.CloneSnapshot(designSnapshot),
            recipe = recipe,
            assignedMechanitor = mechanitor,
            totalTicks = totalTicks,
            ticksRemaining = totalTicks
        };
        return true;
    }

    private bool TryFindAssignedMechanitor(GrayMechDesignSnapshot designSnapshot, RecipeDef recipe, out Pawn mechanitor, out string reason)
    {
        mechanitor = null;
        reason = string.Empty;

        Map map = parent.MapHeld;
        if (map?.mapPawns?.FreeColonists == null)
        {
            reason = "No colonist available.";
            return false;
        }

        List<Pawn> colonists = map.mapPawns.FreeColonists;
        Pawn fallback = null;
        float bandwidthCost = GetBandwidthCost(designSnapshot?.chassis?.ProducedRace);
        for (int i = 0; i < colonists.Count; i++)
        {
            Pawn colonist = colonists[i];
            if (colonist == null || colonist.Dead || colonist.Downed || colonist.MapHeld != map)
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

        Map map = parent.MapHeld;
        if (completedOrder?.designSnapshot?.chassis?.pawnKindDef == null || map == null)
        {
            TryStartNextQueuedOrder();
            return;
        }

        ConsumeCurrentOrderIngredients(completedOrder, map);

        Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            completedOrder.designSnapshot.chassis.pawnKindDef,
            completedOrder.assignedMechanitor?.Faction ?? parent.Faction ?? Faction.OfPlayer,
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

        mech.TryGetComp<CompGrayMechLoadout>()?.ApplyDesign(completedOrder.designSnapshot);

        if (completedOrder.assignedMechanitor != null && !completedOrder.assignedMechanitor.Dead)
        {
            completedOrder.assignedMechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
        }

        IntVec3 spawnCell = parent.InteractionCell;
        if (!spawnCell.IsValid)
        {
            spawnCell = parent.PositionHeld;
        }

        if (!GenPlace.TryPlaceThing(mech, spawnCell, map, ThingPlaceMode.Near, out Thing resultingThing))
        {
            GenPlace.TryPlaceThing(mech, parent.PositionHeld, map, ThingPlaceMode.Near, out resultingThing);
        }

        Messages.Message("Assembly complete: " + (resultingThing ?? mech).LabelCap, parent, MessageTypeDefOf.PositiveEvent);
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

    private void ConsumeCurrentOrderIngredients(GrayMechAssemblyOrder order, Map map)
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
                order.recipe.Worker.ConsumeIngredient(consumedThing, order.recipe, map);
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

    private static RecipeDef FindAssemblyRecipeForSnapshot(GrayMechDesignSnapshot designSnapshot)
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
            if (extension?.chassis == designSnapshot?.chassis)
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
}

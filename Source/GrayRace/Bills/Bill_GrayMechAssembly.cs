using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.DefModExtensions;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Bills;

public class Bill_GrayMechAssembly : Bill_Mech
{
    private GrayMechDesignSnapshot designSnapshot;

    [Unsaved(false)]
    private readonly List<IngredientCount> cachedIngredients = new();

    [Unsaved(false)]
    private readonly List<ThingDefCountClass> cachedCosts = new();

    [Unsaved(false)]
    private bool ingredientCacheDirty = true;

    private Building_WorkTableAutonomous Drydock => billStack?.billGiver as Building_WorkTableAutonomous;

    public bool HasValidDesign
    {
        get
        {
            EnsureDesignSnapshot();
            return designSnapshot?.chassis?.pawnKindDef != null;
        }
    }

    public override float BandwidthCost
    {
        get
        {
            EnsureDesignSnapshot();
            ThingDef producedRace = designSnapshot?.chassis?.ProducedRace;
            if (producedRace != null)
            {
                return producedRace.GetStatValueAbstract(StatDefOf.BandwidthCost);
            }

            return recipe?.ProducedThingDef?.GetStatValueAbstract(StatDefOf.BandwidthCost) ?? 0f;
        }
    }

    public override string Label
    {
        get
        {
            EnsureDesignSnapshot();
            if (designSnapshot != null && !designSnapshot.designLabel.NullOrEmpty())
            {
                return designSnapshot.designLabel;
            }

            return base.Label;
        }
    }

    public Bill_GrayMechAssembly()
    {
    }

    public Bill_GrayMechAssembly(RecipeDef recipe, Precept_ThingStyle precept = null)
        : base(recipe, precept)
    {
        EnsureDesignSnapshot();
    }

    public List<IngredientCount> GetDynamicIngredients()
    {
        EnsureIngredientCache();
        return cachedIngredients;
    }

    public GrayMechDesignSnapshot DesignSnapshot
    {
        get
        {
            EnsureDesignSnapshot();
            return designSnapshot;
        }
    }

    public void SetDesignSnapshot(GrayMechDesignSnapshot snapshot)
    {
        designSnapshot = GrayMechDesignUtility.CloneSnapshot(snapshot);
        if (designSnapshot != null && designSnapshot.designLabel.NullOrEmpty())
        {
            designSnapshot.designLabel = recipe?.label;
        }

        ingredientCacheDirty = true;
    }

    public string RequiredMaterialSummary()
    {
        EnsureIngredientCache();
        if (cachedIngredients.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        for (int i = 0; i < cachedIngredients.Count; i++)
        {
            ThingDef thingDef = cachedIngredients[i].FixedIngredient;
            if (thingDef == null)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(thingDef.LabelCap);
            sb.Append(" x");
            sb.Append(Mathf.CeilToInt(cachedIngredients[i].GetBaseCount()));
        }

        return sb.ToString();
    }

    public override Thing CreateProducts()
    {
        EnsureDesignSnapshot();
        if (designSnapshot?.chassis?.pawnKindDef == null)
        {
            Log.Error("Gray mech assembly bill has no valid chassis or pawn kind.");
            return null;
        }

        Pawn mechanitor = BoundPawn;
        Faction faction = mechanitor?.Faction ?? Faction.OfPlayer;
        Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            designSnapshot.chassis.pawnKindDef,
            faction,
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

        mechanitor?.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
        mech.TryGetComp<CompGrayMechLoadout>()?.ApplyDesign(designSnapshot);
        return mech;
    }

    public override void AppendInspectionData(StringBuilder sb)
    {
        EnsureDesignSnapshot();

        if (designSnapshot?.chassis != null)
        {
            sb.AppendLine("Assembly design: " + (designSnapshot.designLabel.NullOrEmpty() ? designSnapshot.chassis.LabelCap.ToString() : designSnapshot.designLabel));
            sb.AppendLine("Chassis: " + designSnapshot.chassis.LabelCap);

            string moduleSummary = GrayMechDesignUtility.BuildModuleSummary(designSnapshot);
            if (!moduleSummary.NullOrEmpty())
            {
                sb.AppendLine("Modules: " + moduleSummary);
            }
        }

        if (State == FormingState.Gathering)
        {
            AppendCurrentIngredientProgress(sb);
        }
        else if (State == FormingState.Preparing)
        {
            sb.AppendLine("Preparing assembly cycle.");
        }
        else if (State == FormingState.Forming)
        {
            sb.AppendLine("Assembling mech: " + recipe.ProducedThingDef.LabelCap);
        }
        else if (State == FormingState.Formed)
        {
            sb.AppendLine("Assembly complete.");
        }

        base.AppendInspectionData(sb);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref designSnapshot, "designSnapshot");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ingredientCacheDirty = true;
            EnsureDesignSnapshot();
        }
    }

    private void EnsureDesignSnapshot()
    {
        if (designSnapshot != null)
        {
            return;
        }

        DefModExtension_MechAssemblyRecipe extension = recipe?.GetModExtension<DefModExtension_MechAssemblyRecipe>();
        if (extension?.preset == null)
        {
            return;
        }

        designSnapshot = GrayMechDesignUtility.CreateSnapshot(extension.preset);
        if (designSnapshot != null && designSnapshot.designLabel.NullOrEmpty())
        {
            designSnapshot.designLabel = recipe.label;
        }

        ingredientCacheDirty = true;
    }

    private void EnsureIngredientCache()
    {
        if (!ingredientCacheDirty)
        {
            return;
        }

        cachedIngredients.Clear();
        cachedCosts.Clear();
        EnsureDesignSnapshot();
        GrayMechDesignUtility.BuildIngredientList(designSnapshot, cachedIngredients, cachedCosts);
        ingredientCacheDirty = false;
    }

    private void AppendCurrentIngredientProgress(StringBuilder sb)
    {
        EnsureIngredientCache();
        if (cachedIngredients.Count == 0)
        {
            return;
        }

        Building_WorkTableAutonomous drydock = Drydock;
        sb.AppendLine("Required materials:");
        for (int i = 0; i < cachedIngredients.Count; i++)
        {
            ThingDef thingDef = cachedIngredients[i].FixedIngredient;
            if (thingDef == null)
            {
                continue;
            }

            int loaded = drydock?.innerContainer.TotalStackCountOfDef(thingDef) ?? 0;
            int required = Mathf.CeilToInt(cachedIngredients[i].GetBaseCount());
            sb.AppendLine(thingDef.LabelCap + " " + loaded + " / " + required);
        }
    }
}

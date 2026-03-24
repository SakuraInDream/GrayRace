using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Mechs;

public sealed class GrayMechAssemblyOrder : IExposable
{
    public GrayMechDesignSnapshot designSnapshot;
    public RecipeDef recipe;
    public Pawn assignedMechanitor;
    public int ticksRemaining;
    public int totalTicks;

    public string Label => designSnapshot?.designLabel ?? recipe?.label ?? "Gray mech";

    public ThingDef ProducedRace => designSnapshot?.chassis?.ProducedRace ?? recipe?.ProducedThingDef;

    public float ProgressPercent => totalTicks > 0 ? 1f - Mathf.Clamp01(ticksRemaining / (float)totalTicks) : 0f;

    public void ExposeData()
    {
        Scribe_Deep.Look(ref designSnapshot, "designSnapshot");
        Scribe_Defs.Look(ref recipe, "recipe");
        Scribe_References.Look(ref assignedMechanitor, "assignedMechanitor");
        Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
        Scribe_Values.Look(ref totalTicks, "totalTicks", 0);
    }
}

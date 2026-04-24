using RimWorld;
using Verse;
using RimWorld.Planet;

namespace SD.GrayRace.Abilities;

public class Ability_NaniteCost : Ability
{
    public Ability_NaniteCost()
    {
    }

    public Ability_NaniteCost(Pawn pawn) : base(pawn)
    {
    }

    public Ability_NaniteCost(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept)
    {
    }

    public Ability_NaniteCost(Pawn pawn, AbilityDef def) : base(pawn, def)
    {
    }

    public Ability_NaniteCost(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def)
    {
    }

    public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (!TryConsumeNanites())
        {
            return false;
        }

        return base.Activate(target, dest);
    }

    public override bool Activate(GlobalTargetInfo target)
    {
        if (!TryConsumeNanites())
        {
            return false;
        }

        return base.Activate(target);
    }

    private bool TryConsumeNanites()
    {
        AbilityComp_NanitesCost comp = CompOfType<AbilityComp_NanitesCost>();
        if (comp == null || comp.Cost <= float.Epsilon)
        {
            return true;
        }

        if (comp.TryConsume())
        {
            return true;
        }

        if (pawn?.Faction == Faction.OfPlayer)
        {
            Messages.Message("GR_AbilityFailedNoNanites".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput, false);
        }

        return false;
    }
}

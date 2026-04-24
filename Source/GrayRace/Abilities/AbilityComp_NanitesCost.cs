using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Modules;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Abilities;

public class AbilityComp_NanitesCost : AbilityComp
{
    public CompProperties_AbilityNanitesCost Props => (CompProperties_AbilityNanitesCost)props;

    public float Cost => Props.nanitesCost;

    public override bool CanCast => HasEnoughNanitesIncludingQueued;

    private bool HasEnoughNanitesIncludingQueued
    {
        get
        {
            if (Cost <= float.Epsilon)
            {
                return true;
            }

            NaniteModule module = NaniteModule;
            return module != null && module.CurrentNanites + 0.0001f >= Cost + TotalQueuedNanitesCost();
        }
    }

    private NaniteModule NaniteModule
    {
        get
        {
            CompGrayManager manager = parent?.pawn?.GetManager();
            return manager?.naniteModule;
        }
    }

    public override bool GizmoDisabled(out string reason)
    {
        NaniteModule module = NaniteModule;
        if (Cost <= float.Epsilon)
        {
            reason = null;
            return false;
        }

        if (module == null)
        {
            reason = "GR_AbilityDisabledNoNanitesModule".Translate(parent.pawn);
            return true;
        }

        float reserved = TotalQueuedNanitesCost();
        if (module.CurrentNanites + 0.0001f < Cost + reserved)
        {
            reason = "GR_AbilityDisabledNoNanites".Translate(parent.pawn, Cost.ToString("0.#"), (module.CurrentNanites - reserved).ToString("0.#"));
            return true;
        }

        reason = null;
        return false;
    }

    public override string CompInspectStringExtra()
    {
        if (Cost <= float.Epsilon)
        {
            return null;
        }

        return "GR_AbilityNanitesCost".Translate() + ": " + Cost.ToString("0.#");
    }

    public override string ToString()
    {
        return base.ToString() + " cost=" + Cost.ToString("0.#");
    }

    public bool TryConsume()
    {
        NaniteModule module = NaniteModule;
        if (module == null || module.CurrentNanites + 0.0001f < Cost)
        {
            return false;
        }

        module.OffsetNanites(0f - Cost);
        return true;
    }

    public float TotalQueuedNanitesCost()
    {
        Pawn pawn = parent?.pawn;
        if (pawn?.jobs == null)
        {
            return 0f;
        }

        float total = 0f;
        Job currentJob = pawn.jobs.curJob;
        if (currentJob?.verbToUse is Verb_CastAbility currentVerb)
        {
            total += CostOf(currentVerb.ability);
        }

        JobQueue queue = pawn.jobs.jobQueue;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].job.verbToUse is Verb_CastAbility queuedVerb)
            {
                total += CostOf(queuedVerb.ability);
            }
        }

        return total;
    }

    public static float CostOf(Ability ability)
    {
        AbilityComp_NanitesCost comp = ability?.CompOfType<AbilityComp_NanitesCost>();
        return comp?.Cost ?? 0f;
    }
}

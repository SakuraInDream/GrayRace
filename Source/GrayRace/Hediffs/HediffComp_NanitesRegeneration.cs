using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Modules;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Hediffs;

public class HediffComp_NanitesRegeneration : HediffComp
{
    private const int HealingTickInterval = 60;

    private NaniteModule _resNanites;
    private List<Hediff_Injury> _tmpInjuries = new();
    private List<Hediff_MissingPart> _tmpMissingParts = new();

    public bool isRegenerationActive = false;

    public HediffCompProperties_NanitesRegeneration Props => (HediffCompProperties_NanitesRegeneration)props;

    public override void CompPostMake()
    {
        base.CompPostMake();
        _resNanites = Pawn.GetManager().naniteModule;
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref isRegenerationActive, "isRegenerationActive");
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        base.CompPostTickInterval(ref severityAdjustment, delta);
        if (Pawn.Dead)
        {
            return;
        }

        Regen(delta);
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        if (Pawn.Faction == Faction.OfPlayer && (Pawn.Drafted || Pawn.Downed || isRegenerationActive))
        {
            yield return new Command_Toggle
            {
                defaultLabel = "超级修复",
                defaultDesc = "消耗灰潮源质快速修复机体",
                icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/Bleeding"),
                isActive = () => isRegenerationActive,
                toggleAction = () =>
                {
                    isRegenerationActive = !isRegenerationActive;
                    if (isRegenerationActive)
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                    else
                    {
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    }
                }
            };
        }
    }

    private void Regen(int delta)
    {
        if (!Pawn.IsGrayRace())
        {
            return;
        }

        _resNanites ??= Pawn.GetManager().naniteModule;
        if (_resNanites is null)
        {
            return;
        }

        if (isRegenerationActive && _resNanites.CurrentNanites < Props.naniteCostPerSeconds)
        {
            Messages.Message("资源耗尽，修复中止。", Pawn, MessageTypeDefOf.NegativeEvent);
            isRegenerationActive = false;
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }

        if (!Pawn.IsHashIntervalTick(HealingTickInterval, delta))
        {
            return;
        }

        if (!isRegenerationActive)
        {
            DoPassiveHealing();
            return;
        }

        DoActiveHealing();
    }

    private void DoPassiveHealing()
    {
        List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff.Bleeding || hediff.TendableNow())
            {
                hediff.Tended(new FloatRange(0.1f, 0.4f).RandomInRange, 0.4f);
            }
        }
    }

    private void DoActiveHealing()
    {
        List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff.Bleeding || hediff.TendableNow())
            {
                hediff.Tended(1f, 1f);
            }
        }

        if (Pawn.IsHashIntervalTick(HealingTickInterval))
        {
            Pawn.health.hediffSet.GetHediffs(ref _tmpInjuries);
            for (int i = 0; i < _tmpInjuries.Count; i++)
            {
                Hediff_Injury hediffInjury = _tmpInjuries[i];
                hediffInjury.Heal(Props.healAmountPerSeconds);
                Pawn.health.hediffSet.Notify_Regenerated(Props.healAmountPerSeconds);
            }

            if (!HasAnyHealableCondition())
            {
                isRegenerationActive = false;
                Messages.Message("无伤口，机体修复已中止。", Pawn, MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
                return;
            }
        }

        if (Pawn.IsHashIntervalTick(HealingTickInterval * 10))
        {
            Pawn.health.hediffSet.GetHediffs(
                ref _tmpMissingParts,
                h => h.Part.parent != null
                    && !HasInjuryOnPart(h.Part.parent)
                    && Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null
                    && Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null);

            for (int i = 0; i < _tmpMissingParts.Count; i++)
            {
                Hediff_MissingPart hediffMissingPart = _tmpMissingParts[i];
                BodyPartRecord part = hediffMissingPart.Part;
                Pawn.health.RemoveHediff(hediffMissingPart);
                Hediff hediff = Pawn.health.AddHediff(HediffDefOf.Misc, part);
                float partHealth = Pawn.health.hediffSet.GetPartHealth(part);

                hediff.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);
                Pawn.health.hediffSet.Notify_Regenerated(partHealth - hediff.Severity);
            }
        }
    }

    private bool HasAnyHealableCondition()
    {
        Pawn.health.hediffSet.GetHediffs(ref _tmpInjuries);
        for (int i = 0; i < _tmpInjuries.Count; i++)
        {
            if (_tmpInjuries[i] != null)
            {
                return true;
            }
        }

        Pawn.health.hediffSet.GetHediffs(ref _tmpMissingParts);
        for (int i = 0; i < _tmpMissingParts.Count; i++)
        {
            Hediff_MissingPart missingPart = _tmpMissingParts[i];
            if (missingPart?.Part?.parent == null)
            {
                continue;
            }

            BodyPartRecord parent = missingPart.Part.parent;
            if (HasInjuryOnPart(parent))
            {
                continue;
            }

            if (Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(parent) != null)
            {
                continue;
            }

            if (Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(parent) != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasInjuryOnPart(BodyPartRecord part)
    {
        if (part == null || _tmpInjuries.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _tmpInjuries.Count; i++)
        {
            if (_tmpInjuries[i]?.Part == part)
            {
                return true;
            }
        }

        return false;
    }
}

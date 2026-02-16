using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Modules;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Hediffs;

public class HediffComp_NanitesRegeneration : HediffComp
{
    // private CompResource_Nanites _resNanites;
    private NaniteModule _resNanites;

    public bool isRegenerationActive = false;

    private List<Hediff_Injury> _tmpInjuries = new List<Hediff_Injury>();
    private List<Hediff_MissingPart> _tmpMissingParts = new List<Hediff_MissingPart>();

    private const int HealingTickInterval = 60;

    public HediffCompProperties_NanitesRegeneration Props => (HediffCompProperties_NanitesRegeneration)props;

    public override void CompPostMake()
    {
        base.CompPostMake();
        _resNanites = Pawn.GetManager().naniteModule; // Pawn.TryGetComp<CompResource_Nanites>();
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref isRegenerationActive, "isRegenerationActive");
    }


    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        base.CompPostTickInterval(ref severityAdjustment, delta);
        if (Pawn.Dead) return;

        Regen(delta);
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        if (Pawn.Faction == Faction.OfPlayer && (Pawn.Drafted || Pawn.Downed || isRegenerationActive))
        {
            yield return new Command_Toggle
            {
                defaultLabel = "超级修复", // 待本地化
                defaultDesc = "消耗灰潮源质快速修复机体", // 待本地化
                icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/Bleeding"), // 图标要换
                isActive = () => isRegenerationActive,
                toggleAction = () =>
                {
                    isRegenerationActive = !isRegenerationActive;
                    if (isRegenerationActive) SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    else SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
            };
        }
    }


    private void Regen(int delta)
    {
        if (!Pawn.IsGrayRace()) return;

        _resNanites ??= Pawn.GetManager().naniteModule; // Pawn.TryGetComp<CompResource_Nanites>();
        if (_resNanites is null) return;

        if (isRegenerationActive && _resNanites.CurrentNanites < Props.naniteCostPerSeconds)
        {
            Messages.Message("资源耗尽，修复中止。", Pawn, MessageTypeDefOf.NegativeEvent);
            isRegenerationActive = false;
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }

        if (!isRegenerationActive)
        {
            if (Pawn.IsHashIntervalTick(HealingTickInterval, delta))
            {
                DoPassiveHealing();
            }
        }
        else
        {
            if (Pawn.IsHashIntervalTick(HealingTickInterval, delta))
            {
                DoActiveHealing();
            }
        }
    }

    // 被动只做紧急止血
    private void DoPassiveHealing()
    {
        List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
        foreach (Hediff hediff in hediffs)
        {
            if (hediff.Bleeding || hediff.TendableNow())
            {
                hediff.Tended(new FloatRange(0.1f, 0.4f).RandomInRange, 0.4f);
            }
        }
    }

    // 治疗和断肢再生
    private void DoActiveHealing()
    {
        List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
        // 疯狂高质量包扎
        foreach (Hediff hediff in hediffs)
        {
            if (hediff.Bleeding || hediff.TendableNow())
            {
                hediff.Tended(1f, 1f);
            }
        }
        // 疯狂恢复
        if (Pawn.IsHashIntervalTick(HealingTickInterval))
        {
            Pawn.health.hediffSet.GetHediffs<Hediff_Injury>(ref _tmpInjuries);
            foreach (Hediff_Injury hediffInjury in _tmpInjuries)
            {
                hediffInjury.Heal(Props.healAmountPerSeconds);

                Pawn.health.hediffSet.Notify_Regenerated(Props.healAmountPerSeconds);
            }

            if (!HasAnyHealableCondition())
            {
                isRegenerationActive = false;
                Messages.Message("无伤口，机体修复已中止。", Pawn, MessageTypeDefOf.PositiveEvent); // 待本地化
                SoundDefOf.Click.PlayOneShotOnCamera();
                return;
            }
        }

        if (Pawn.IsHashIntervalTick(HealingTickInterval * 10))
        {
            // 化繁为简，重新使用原版食尸鬼的再生逻辑
            Pawn.health.hediffSet.GetHediffs(ref _tmpMissingParts,
                h => h.Part.parent != null && !_tmpInjuries.Any(x => x.Part == h.Part.parent) &&
                     Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null &&
                     Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null);

            foreach (Hediff_MissingPart hediffMissingPart in _tmpMissingParts)
            {
                BodyPartRecord part = hediffMissingPart.Part;
                Pawn.health.RemoveHediff(hediffMissingPart);
                Hediff hediff2 = Pawn.health.AddHediff(HediffDefOf.Misc, part);
                float partHealth = Pawn.health.hediffSet.GetPartHealth(part);

                hediff2.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);
                Pawn.health.hediffSet.Notify_Regenerated(partHealth - hediff2.Severity);
            }
        }
    }

    private bool HasAnyHealableCondition()
    {
        List<Hediff> allHediffs = Pawn.health.hediffSet.hediffs;
        if (allHediffs.Any(h => h is Hediff_Injury or Hediff_MissingPart))
        {
            return true;
        }
        return false;
    }
}

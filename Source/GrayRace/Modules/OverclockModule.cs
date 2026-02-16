using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps.PropertiesSettings;
using SD.GrayRace.Defs;
using SD.GrayRace.Hediffs;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Modules;

public class OverclockModule : GrayModuleBase
{
    private Dictionary<BodyPartRecord, float> _overclockData = new Dictionary<BodyPartRecord, float>();
    public OverclockSetting Setting => manager.Props.overclockSetting;
    public Pawn Pawn => pawn;
    public float BaseOverclockCap => GrayRaceDefOf.GRStat_OverclockMaxLevel.defaultBaseValue;

    public float CachedEnergyConsumptionFactor { get; private set; } = 0f;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref _overclockData, "overclockData", LookMode.BodyPart, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _overclockData ??= new Dictionary<BodyPartRecord, float>();
            RecalculateTotalDrain();
        }
    }

    public float GetOverclockLevel(BodyPartRecord part)
    {
        return _overclockData.TryGetValue(part, out float val) ? val : 0f;
    }

    public void SetOverclockLevel(BodyPartRecord part, float level)
    {
        float maxLevel = GetMaxOverclockLevel(part);
        level = Mathf.Clamp(level, 0f, maxLevel);

        if (level <= 0.001f)
        {
            if (_overclockData.Remove(part))
            {
                RemoveOverclockHediff(part);
            }
        }
        else
        {
            _overclockData[part] = level;
            ApplyOverclockHediff(part, level);
        }

        RecalculateTotalDrain();
    }

    public float GetMaxOverclockLevel(BodyPartRecord part)
    {
        if (part == null || Pawn?.health?.hediffSet == null) return 0f;
        return Mathf.Max(0f, Pawn.GetStatValue(GrayRaceDefOf.GRStat_OverclockMaxLevel));
    }

    private void ApplyOverclockHediff(BodyPartRecord part, float level)
    {
        HediffDef def = GetHediffDefForPart(part);
        if (def == null) return;

        Hediff hediff = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.Part == part && h.def == def);
        if (hediff == null)
        {
            hediff = HediffMaker.MakeHediff(def, Pawn, part);
            Pawn.health.AddHediff(hediff);
        }

        float oldSeverity = hediff.Severity;
        hediff.Severity = level;
        if (Mathf.Abs(oldSeverity - level) > 0.0001f)
        {
            Pawn.health.Notify_HediffChanged(hediff);
        }
    }

    private void RemoveOverclockHediff(BodyPartRecord part)
    {
        HediffDef def = GetHediffDefForPart(part);
        if (def == null) return;

        Hediff hediff = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.Part == part && h.def == def);
        if (hediff != null)
        {
            Pawn.health.RemoveHediff(hediff);
        }
    }

    private void RecalculateTotalDrain()
    {
        float totalFactor = 0f;
        var hediffs = Pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i] is Hediff_Overclock ovHediff)
            {
                float factor = ovHediff.Def.baseEnergyDrain * ovHediff.Severity;

                if (OverclockUtility.IsCorePart(ovHediff.Part))
                {
                    factor *= 2f;
                }

                totalFactor += factor;
            }
        }

        CachedEnergyConsumptionFactor = totalFactor;
    }

    public HediffDef GetHediffDefForPart(BodyPartRecord part)
    {
        if (Setting.overclockMaps != null)
        {
            foreach (OverclockSetting.OverclockMap map in Setting.overclockMaps)
            {
                if (map.part != null && map.part == part.def) return map.hediff;
                if (map.tag != null && part.def.tags.Contains(map.tag)) return map.hediff;
            }
        }

        return null;
    }
}

using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.Comps;

public class CompGrayMechLoadout : ThingComp
{
    private const string ModuleStatsExplanationHeader = "舰船组件";
    private GrayMechDesignSnapshot designSnapshot;
    private Dictionary<StatDef, float> cachedModuleStatOffsets;
    private Dictionary<StatDef, float> cachedModuleStatFactors;

    private Pawn Pawn => parent as Pawn;

    public GrayMechDesignSnapshot DesignSnapshot => designSnapshot;

    public bool HasDesign => designSnapshot?.chassis != null;

    public void ApplyDesign(GrayMechDesignSnapshot snapshot)
    {
        ClearAffectedStatCaches(designSnapshot);
        designSnapshot = GrayMechDesignUtility.CloneSnapshot(snapshot);
        cachedModuleStatOffsets = null;
        cachedModuleStatFactors = null;
        ClearAffectedStatCaches(designSnapshot);
        if (Pawn != null && designSnapshot != null)
        {
            GrayMechModuleApplier.ApplyLoadout(Pawn, designSnapshot);
            Pawn.TryGetComp<CompGrayMechVanillaShield>()?.Notify_LoadoutChanged();
            Pawn.TryGetComp<CompGrayMechSystems>()?.Notify_LoadoutChanged();
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        ClearAffectedStatCaches(designSnapshot);
        if (!respawningAfterLoad && Pawn != null && designSnapshot == null && GrayMechRandomLoadoutGenerator.TryCreateSnapshotFor(Pawn, out GrayMechDesignSnapshot generatedSnapshot))
        {
            ApplyDesign(generatedSnapshot);
            return;
        }

        if (Pawn != null && designSnapshot != null)
        {
            GrayMechModuleApplier.ApplyLoadout(Pawn, designSnapshot);
            Pawn.TryGetComp<CompGrayMechVanillaShield>()?.Notify_LoadoutChanged();
            Pawn.TryGetComp<CompGrayMechSystems>()?.Notify_LoadoutChanged();
        }
    }

    public override string CompInspectStringExtra()
    {
        return string.Empty;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref designSnapshot, "designSnapshot");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            cachedModuleStatOffsets = null;
            cachedModuleStatFactors = null;
        }
    }

    public float GetModuleStatOffset(StatDef stat)
    {
        if (stat == null || designSnapshot?.modules == null)
        {
            return 0f;
        }

        EnsureStatOffsetCache();
        return cachedModuleStatOffsets != null && cachedModuleStatOffsets.TryGetValue(stat, out float value) ? value : 0f;
    }

    public float GetModuleStatFactor(StatDef stat)
    {
        if (stat == null || designSnapshot?.modules == null)
        {
            return 1f;
        }

        EnsureStatCaches();
        return cachedModuleStatFactors != null && cachedModuleStatFactors.TryGetValue(stat, out float value) ? value : 1f;
    }

    public override float GetStatOffset(StatDef stat)
    {
        return GetModuleStatOffset(stat);
    }

    public override float GetStatFactor(StatDef stat)
    {
        return GetModuleStatFactor(stat);
    }

    public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
    {
        if (stat == null || sb == null || designSnapshot?.modules == null)
        {
            return;
        }

        bool wroteHeader = false;
        for (int i = 0; i < designSnapshot.modules.Count; i++)
        {
            GRMechModuleDef module = designSnapshot.modules[i]?.module;
            if (module == null)
            {
                continue;
            }

            float offset = GetStatOffsetFromList(module.statOffsets, stat);
            if (offset != 0f)
            {
                AppendExplanationLine(sb, whitespace, module, stat, offset, ToStringNumberSense.Offset, ref wroteHeader);
            }

            float factor = GetStatFactorFromList(module.statFactors, stat);
            if (factor != 1f)
            {
                AppendExplanationLine(sb, whitespace, module, stat, factor, ToStringNumberSense.Factor, ref wroteHeader);
            }
        }
    }

    private void EnsureStatOffsetCache()
    {
        EnsureStatCaches();
    }

    private void ClearAffectedStatCaches(GrayMechDesignSnapshot snapshot)
    {
        if (Pawn == null || snapshot?.modules == null)
        {
            return;
        }

        HashSet<StatDef> clearedStats = new();
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GRMechModuleDef module = snapshot.modules[i]?.module;
            ClearAffectedStatCaches(module?.statOffsets, clearedStats);
            ClearAffectedStatCaches(module?.statFactors, clearedStats);
        }
    }

    private void ClearAffectedStatCaches(List<StatModifier> modifiers, HashSet<StatDef> clearedStats)
    {
        if (modifiers == null)
        {
            return;
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatDef stat = modifiers[i]?.stat;
            if (stat != null && clearedStats.Add(stat))
            {
                stat.Worker.ClearCacheForThing(Pawn);
            }
        }
    }

    private void EnsureStatCaches()
    {
        if (cachedModuleStatOffsets != null && cachedModuleStatFactors != null)
        {
            return;
        }

        cachedModuleStatOffsets = new Dictionary<StatDef, float>();
        cachedModuleStatFactors = new Dictionary<StatDef, float>();
        if (designSnapshot?.modules == null)
        {
            return;
        }

        for (int i = 0; i < designSnapshot.modules.Count; i++)
        {
            var module = designSnapshot.modules[i]?.module;
            List<StatModifier> offsets = module?.statOffsets;
            if (offsets != null)
            {
                for (int j = 0; j < offsets.Count; j++)
                {
                    StatModifier modifier = offsets[j];
                    if (modifier?.stat == null || modifier.value == 0f)
                    {
                        continue;
                    }

                    if (cachedModuleStatOffsets.TryGetValue(modifier.stat, out float current))
                    {
                        cachedModuleStatOffsets[modifier.stat] = current + modifier.value;
                    }
                    else
                    {
                        cachedModuleStatOffsets.Add(modifier.stat, modifier.value);
                    }
                }
            }

            List<StatModifier> factors = module?.statFactors;
            if (factors == null)
            {
                continue;
            }

            for (int j = 0; j < factors.Count; j++)
            {
                StatModifier modifier = factors[j];
                if (modifier?.stat == null || modifier.value == 1f)
                {
                    continue;
                }

                if (cachedModuleStatFactors.TryGetValue(modifier.stat, out float current))
                {
                    cachedModuleStatFactors[modifier.stat] = current * modifier.value;
                }
                else
                {
                    cachedModuleStatFactors.Add(modifier.stat, modifier.value);
                }
            }
        }
    }

    private static float GetStatOffsetFromList(List<StatModifier> modifiers, StatDef stat)
    {
        if (modifiers == null || stat == null)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            if (modifier?.stat == stat)
            {
                total += modifier.value;
            }
        }

        return total;
    }

    private static float GetStatFactorFromList(List<StatModifier> modifiers, StatDef stat)
    {
        if (modifiers == null || stat == null)
        {
            return 1f;
        }

        float total = 1f;
        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            if (modifier?.stat == stat)
            {
                total *= modifier.value;
            }
        }

        return total;
    }

    private static void AppendExplanationLine(StringBuilder sb, string whitespace, GRMechModuleDef module, StatDef stat, float value, ToStringNumberSense numberSense, ref bool wroteHeader)
    {
        if (!wroteHeader)
        {
            sb.AppendLine(whitespace + ModuleStatsExplanationHeader + ":");
            wroteHeader = true;
        }

        sb.AppendLine(whitespace + "    " + module.LabelCap + ": " + stat.Worker.ValueToString(value, finalized: false, numberSense));
    }
}

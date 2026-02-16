using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Comps.PropertiesSettings;
using SD.GrayRace.Needs;
using SD.GrayRace.UISet.Gizmos;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Modules;

public class NaniteModule: GrayModuleBase
{
    private float _curNanites;
    public NanitesSetting Settings => manager.Props.nanitesSetting;
    private Gizmo_NaniteModule _gizmo;

    public Pawn Pawn => pawn;

    // public Need_GrayRaceEnergy EnergyNeed => pawn.needs.TryGetNeed<Need_GrayRaceEnergy>();
    public override void Initialize(CompGrayManager mgr, Pawn p)
    {
        base.Initialize(mgr, p);
        if (Pawn.IsColonistPlayerControlled && Pawn.IsGrayRace())
        {
            _gizmo = new Gizmo_NaniteModule(this);
        }
    }

    public float Max => pawn.GetStatValue(GrayRaceDefOf.GRStat_NaniteMax);

    public float CurrentNanites
    {
        get => _curNanites;
        set => _curNanites = value;
    }

    public float CurrentNanitesPercent => Max > 0 ? _curNanites / Max : 0f;


    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        _gizmo ??= new Gizmo_NaniteModule(this);

        _curNanites = Mathf.Clamp(_curNanites, 0f, Max);
        EnsureRegenHediff();
    }

    public override void CompTick()
    {
        base.CompTick();
        if (Pawn.IsHashIntervalTick(60))
        {
            TickCal();
        }
    }

    private void TickCal()
    {
        float regenAmount = Pawn.GetStatValue(GrayRaceDefOf.GRStat_NaniteRegenRate);
        _curNanites = Mathf.Min(_curNanites + regenAmount, Max);
    }

    private void EnsureRegenHediff()
    {
        if (!Pawn.health.hediffSet.HasHediff(GrayRaceDefOf.NanitesRegeneration))
        {
            Pawn.health.AddHediff(GrayRaceDefOf.NanitesRegeneration);
        }
    }

    public void OffsetNanites(float amount)
    {
        _curNanites = Mathf.Clamp(_curNanites + amount, 0f, Max);
    }
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (Pawn.IsColonistPlayerControlled && Pawn.IsGrayRace())
        {
            _gizmo ??= new Gizmo_NaniteModule(this);
            yield return _gizmo;
        }

        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEBUG: -10 Nanites", action = () => OffsetNanites(-10f)
            };
            yield return new Command_Action
            {
                defaultLabel = "DEBUG: Fill Nanites", action = () => OffsetNanites(Max)
            };
            yield return new Command_Action
            {
                defaultLabel = "DEBUG: +10 Nanites", action = () => OffsetNanites(10f)
            };
        }
    }
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _curNanites, "curNanites");
    }
}

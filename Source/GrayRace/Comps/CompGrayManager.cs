using System.Collections.Generic;
using System.Linq;
using System.Text;
using SD.GrayRace.Modules;
using Verse;

namespace SD.GrayRace.Comps;

public class CompGrayManager: ThingComp
{
    public NaniteModule naniteModule = new NaniteModule();
    public OverclockModule overclockModule = new OverclockModule();
    public UpgradeModule upgradeModule = new UpgradeModule();

    private List<GrayModuleBase> _modules;

    public Pawn Pawn => parent as Pawn;
    public CompPropertiesGrayManager Props => props as CompPropertiesGrayManager;

    public override void Initialize(CompProperties properties)
    {
        base.Initialize(properties);
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        _modules = new List<GrayModuleBase>() { naniteModule, overclockModule, upgradeModule };
        foreach (GrayModuleBase module in _modules)
        {
            module.Initialize(this, Pawn);
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        foreach (GrayModuleBase module in _modules)
        {
            module.CompTick();
        }
    }

    public override void CompTickInterval(int delta) => base.CompTickInterval(delta);
    public override void CompTickRare() => base.CompTickRare();
    public override void CompTickLong() => base.CompTickLong();

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        foreach (var gizmo in _modules.SelectMany(module => module.CompGetGizmosExtra()))
        {
            yield return gizmo;
        }
    }

    public override string CompTipStringExtra()
    {
        StringBuilder sb = new StringBuilder(base.CompTipStringExtra());
        foreach (GrayModuleBase module in _modules)
        {
            string text = module.CompInspectStringExtra();
            if (text.NullOrEmpty()) continue;

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        EnsureInitialized();
        foreach (GrayModuleBase module in _modules)
        {
            module.PostExposeData();
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        foreach (GrayModuleBase module in _modules)
        {
            module.PostSpawnSetup(respawningAfterLoad);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using SD.GrayRace.Comps;
using Verse;

namespace SD.GrayRace.Modules;

public abstract class GrayModuleBase
{
    protected Pawn pawn;
    protected CompGrayManager manager;
    public virtual void Initialize(CompGrayManager mgr, Pawn p)
    {
        manager = mgr;
        pawn = p;
    }
    public virtual void PostSpawnSetup(bool respawningAfterLoad) { }
    public virtual void CompTick() { }
    public virtual IEnumerable<Gizmo> CompGetGizmosExtra() => Enumerable.Empty<Gizmo>();
    public virtual string CompInspectStringExtra() => string.Empty;
    public virtual void PostExposeData() { }

}

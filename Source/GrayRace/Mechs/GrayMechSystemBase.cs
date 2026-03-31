using System.Collections.Generic;
using SD.GrayRace.Comps;
using Verse;

namespace SD.GrayRace.Mechs;

public abstract class GrayMechSystemBase
{
    protected CompGrayMechSystems manager;
    protected Pawn pawn;

    protected ThingWithComps Parent => manager?.parent;

    public virtual void Initialize(CompGrayMechSystems mgr, Pawn mech)
    {
        manager = mgr;
        pawn = mech;
    }

    public virtual void Notify_LoadoutChanged()
    {
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
    }

    public virtual void CompTick()
    {
    }

    public virtual IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield break;
    }

    public virtual string CompInspectStringExtra()
    {
        return string.Empty;
    }

    public virtual void PostExposeData()
    {
    }

    public virtual void PostPreApplyDamage(ref DamageInfo dinfo, ref bool absorbed)
    {
    }

    public virtual void PostDraw()
    {
    }

    public virtual void PostDrawExtraSelectionOverlays()
    {
    }

    public virtual void PostDestroy(DestroyMode mode, Map previousMap)
    {
    }
}

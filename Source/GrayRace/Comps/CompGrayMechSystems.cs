using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.Comps;

public class CompGrayMechSystems : ThingComp
{
    private static readonly FieldInfo[] SystemFields = typeof(CompGrayMechSystems)
        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public GrayMechTurretBankSystem turretBankSystem = new();

    private List<GrayMechSystemBase> registeredSystems;

    public Pawn Pawn => parent as Pawn;
    public CompPropertiesGrayMechSystems Props => props as CompPropertiesGrayMechSystems;
    public GrayMechTurretBankSystem TurretBankSystem => turretBankSystem;

    public override void Initialize(CompProperties properties)
    {
        base.Initialize(properties);
        EnsureInitialized();
    }

    public void Notify_LoadoutChanged()
    {
        EnsureInitialized();
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].Notify_LoadoutChanged();
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].CompTick();
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        for (int i = 0; i < registeredSystems.Count; i++)
        {
            foreach (Gizmo gizmo in registeredSystems[i].CompGetGizmosExtra())
            {
                yield return gizmo;
            }
        }
    }

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = null;
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            string text = registeredSystems[i].CompInspectStringExtra();
            if (text.NullOrEmpty())
            {
                continue;
            }

            sb ??= new StringBuilder();
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(text);
        }

        return sb?.ToString() ?? string.Empty;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        EnsureInitialized();
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].PostExposeData();
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].PostSpawnSetup(respawningAfterLoad);
        }
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        base.PostPreApplyDamage(ref dinfo, out absorbed);
        if (absorbed)
        {
            return;
        }

        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].PostPreApplyDamage(ref dinfo, ref absorbed);
            if (absorbed)
            {
                return;
            }
        }
    }

    public override void PostDraw()
    {
        base.PostDraw();
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].PostDraw();
        }
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        base.PostDrawExtraSelectionOverlays();
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].PostDrawExtraSelectionOverlays();
        }
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].PostDestroy(mode, previousMap);
        }
    }

    private void EnsureInitialized()
    {
        registeredSystems ??= new List<GrayMechSystemBase>(2);
        registeredSystems.Clear();
        for (int i = 0; i < SystemFields.Length; i++)
        {
            if (SystemFields[i].GetValue(this) is GrayMechSystemBase system)
            {
                registeredSystems.Add(system);
            }
        }

        for (int i = 0; i < registeredSystems.Count; i++)
        {
            registeredSystems[i].Initialize(this, Pawn);
        }
    }
}

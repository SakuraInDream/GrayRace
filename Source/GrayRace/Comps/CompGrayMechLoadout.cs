using System.Text;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.Comps;

public class CompGrayMechLoadout : ThingComp
{
    private GrayMechDesignSnapshot designSnapshot;

    private Pawn Pawn => parent as Pawn;

    public GrayMechDesignSnapshot DesignSnapshot => designSnapshot;

    public bool HasDesign => designSnapshot?.chassis != null;

    public void ApplyDesign(GrayMechDesignSnapshot snapshot)
    {
        designSnapshot = GrayMechDesignUtility.CloneSnapshot(snapshot);
        if (Pawn != null && designSnapshot != null)
        {
            GrayMechModuleApplier.ApplyLoadout(Pawn, designSnapshot);
        }
    }

    public override string CompInspectStringExtra()
    {
        if (designSnapshot?.chassis == null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        sb.Append("Design: ");
        sb.Append(designSnapshot.designLabel.NullOrEmpty() ? designSnapshot.chassis.LabelCap : designSnapshot.designLabel);

        string moduleSummary = GrayMechDesignUtility.BuildModuleSummary(designSnapshot);
        if (!moduleSummary.NullOrEmpty())
        {
            sb.AppendLine();
            sb.Append("Modules: ");
            sb.Append(moduleSummary);
        }

        return sb.ToString();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref designSnapshot, "designSnapshot");
    }
}

using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace SD.GrayRace.Mechs;

public class WorldComponent_GrayMechDesignLibrary : WorldComponent
{
    private List<GrayMechDesignRecord> designs = new();
    private int nextDesignId = 1;
    private int version;

    public List<GrayMechDesignRecord> Designs => designs;

    public int Version => version;

    public WorldComponent_GrayMechDesignLibrary(World world)
        : base(world)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref designs, "designs", LookMode.Deep);
        Scribe_Values.Look(ref nextDesignId, "nextDesignId", 1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            designs ??= new List<GrayMechDesignRecord>();
            if (nextDesignId < 1)
            {
                nextDesignId = 1;
            }

            for (int i = 0; i < designs.Count; i++)
            {
                GrayMechDesignRecord design = designs[i];
                if (design != null && design.id >= nextDesignId)
                {
                    nextDesignId = design.id + 1;
                }
            }
        }
    }

    public GrayMechDesignRecord GetDesign(int id)
    {
        for (int i = 0; i < designs.Count; i++)
        {
            GrayMechDesignRecord design = designs[i];
            if (design != null && design.id == id)
            {
                return design;
            }
        }

        return null;
    }

    public bool ContainsLabel(string label, int ignoreId = -1)
    {
        if (label.NullOrEmpty())
        {
            return false;
        }

        for (int i = 0; i < designs.Count; i++)
        {
            GrayMechDesignRecord design = designs[i];
            if (design == null || design.id == ignoreId)
            {
                continue;
            }

            if (string.Equals(design.label, label, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string MakeUniqueLabel(string baseLabel, int ignoreId = -1)
    {
        string root = baseLabel.NullOrEmpty() ? "Gray mech design" : baseLabel;
        if (!ContainsLabel(root, ignoreId))
        {
            return root;
        }

        for (int i = 2; i < 1000; i++)
        {
            string candidate = root + " " + i;
            if (!ContainsLabel(candidate, ignoreId))
            {
                return candidate;
            }
        }

        return root + " " + Find.TickManager.TicksGame;
    }

    public GrayMechDesignRecord CreateDesign(GrayMechDesignSnapshot snapshot, string preferredLabel)
    {
        GrayMechDesignRecord record = new()
        {
            id = nextDesignId++,
            label = MakeUniqueLabel(preferredLabel),
            snapshot = GrayMechDesignUtility.CloneSnapshot(snapshot) ?? new GrayMechDesignSnapshot()
        };

        designs.Add(record);
        version++;
        return record;
    }

    public bool OverwriteDesign(int id, GrayMechDesignSnapshot snapshot)
    {
        GrayMechDesignRecord design = GetDesign(id);
        if (design == null)
        {
            return false;
        }

        design.snapshot = GrayMechDesignUtility.CloneSnapshot(snapshot) ?? new GrayMechDesignSnapshot();
        version++;
        return true;
    }

    public bool DeleteDesign(int id)
    {
        for (int i = 0; i < designs.Count; i++)
        {
            if (designs[i] != null && designs[i].id == id)
            {
                designs.RemoveAt(i);
                version++;
                return true;
            }
        }

        return false;
    }

    public void Notify_DesignRenamed()
    {
        version++;
    }
}

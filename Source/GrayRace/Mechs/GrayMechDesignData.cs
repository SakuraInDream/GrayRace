using System.Collections.Generic;
using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.Mechs;

public class GrayMechSectionSelection : IExposable
{
    public GRMechSectionRoleDef role;
    public GRMechSectionLayoutDef layout;

    public void ExposeData()
    {
        Scribe_Defs.Look(ref role, "role");
        Scribe_Defs.Look(ref layout, "layout");
    }
}

public class GrayMechModuleAssignment : IExposable
{
    public string slotKey;
    public GRMechModuleDef module;

    public void ExposeData()
    {
        Scribe_Values.Look(ref slotKey, "slotKey");
        Scribe_Defs.Look(ref module, "module");
    }
}

public class GrayMechDesignSnapshot : IExposable
{
    public string designLabel;
    public GRMechChassisDef chassis;
    public List<GrayMechSectionSelection> sections = new();
    public List<GrayMechModuleAssignment> modules = new();

    public void ExposeData()
    {
        Scribe_Values.Look(ref designLabel, "designLabel");
        Scribe_Defs.Look(ref chassis, "chassis");
        Scribe_Collections.Look(ref sections, "sections", LookMode.Deep);
        Scribe_Collections.Look(ref modules, "modules", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            sections ??= new List<GrayMechSectionSelection>();
            modules ??= new List<GrayMechModuleAssignment>();
        }
    }
}

public class GrayMechResolvedSlot
{
    public GRMechSectionRoleDef role;
    public GRMechSectionLayoutDef layout;
    public GRMechSlotDef slot;
}

public class GrayMechDesignRecord : IExposable, IRenameable
{
    public int id;
    public string label;
    public GrayMechDesignSnapshot snapshot = new();
    public string sourcePresetDefName;

    public string RenamableLabel
    {
        get => label;
        set => label = value;
    }

    public string BaseLabel => "Gray mech design";

    public string InspectLabel => RenamableLabel;

    public void ExposeData()
    {
        Scribe_Values.Look(ref id, "id", 0);
        Scribe_Values.Look(ref label, "label");
        Scribe_Values.Look(ref sourcePresetDefName, "sourcePresetDefName");
        Scribe_Deep.Look(ref snapshot, "snapshot");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            snapshot ??= new GrayMechDesignSnapshot();
        }
    }
}

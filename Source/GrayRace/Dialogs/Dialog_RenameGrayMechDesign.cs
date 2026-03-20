using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.Dialogs;

public class Dialog_RenameGrayMechDesign : Dialog_Rename<GrayMechDesignRecord>
{
    private readonly WorldComponent_GrayMechDesignLibrary library;

    public Dialog_RenameGrayMechDesign(GrayMechDesignRecord design, WorldComponent_GrayMechDesignLibrary library)
        : base(design)
    {
        this.library = library;
    }

    protected override AcceptanceReport NameIsValid(string name)
    {
        AcceptanceReport result = base.NameIsValid(name);
        if (!result.Accepted)
        {
            return result;
        }

        if (library != null && library.ContainsLabel(name, renaming?.id ?? -1))
        {
            return "A mech design with that name already exists.";
        }

        return true;
    }

    protected override void OnRenamed(string name)
    {
        library?.Notify_DesignRenamed();
    }
}

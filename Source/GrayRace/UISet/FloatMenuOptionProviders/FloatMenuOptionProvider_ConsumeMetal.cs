using RimWorld;
using Verse;
using Verse.AI;

namespace SD.GrayRace.UISet.FloatMenuOptionProviders
{
    // 为金属物品添加“享用”选项
    public class FloatMenuOptionProvider_ConsumeMetal: FloatMenuOptionProvider
    {
        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if(clickedThing.def.category != ThingCategory.Item || !clickedThing.def.IsMetal)
            {
                return null;
            }

            if (!context.FirstSelectedPawn.IsGrayRace()) return null;

            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ConsumeThing".Translate(clickedThing.LabelCap, clickedThing), () =>
            {
                Job job = JobMaker.MakeJob(GrayRaceDefOf.GR_ConsumeMetal, clickedThing);
                context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }), context.FirstSelectedPawn, clickedThing);
        }

        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;


    }
}

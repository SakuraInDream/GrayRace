using Verse;

namespace SD.GrayRace.ThingClasses
{
    /// <summary>
    /// 纳米变形器队列中的一个生产任务。灰潮源质在加入队列时已经扣除，
    /// 因此任务只记录产物、数量、成本和工作量。
    /// </summary>
    public class NaniteTransmuterTask : IExposable
    {
        public ThingDef product;
        public int count;
        public float cost;
        public long totalWork;
        public long progressTicks;

        public float ProgressPercent => totalWork > 0L
            ? (float)((double)progressTicks / totalWork)
            : 0f;

        public long RemainingTicks => totalWork > progressTicks
            ? totalWork - progressTicks
            : 0L;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref product, "product");
            Scribe_Values.Look(ref count, "count", 100);
            Scribe_Values.Look(ref cost, "cost", 0f);
            Scribe_Values.Look(ref totalWork, "totalWork", 0L);
            Scribe_Values.Look(ref progressTicks, "progressTicks", 0L);
        }
    }
}

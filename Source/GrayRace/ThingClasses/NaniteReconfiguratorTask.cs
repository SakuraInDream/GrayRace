using Verse;

namespace SD.GrayRace.ThingClasses
{
    /// <summary>
    /// 纳米重塑台队列中的一个分解任务。输入材料在加入队列时已经从地图扣除。
    /// </summary>
    public class NaniteReconfiguratorTask : IExposable
    {
        public ThingDef material;
        public int count;
        public int outputNanites;
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
            Scribe_Defs.Look(ref material, "material");
            Scribe_Values.Look(ref count, "count", 100);
            Scribe_Values.Look(ref outputNanites, "outputNanites", 0);
            Scribe_Values.Look(ref totalWork, "totalWork", 0L);
            Scribe_Values.Look(ref progressTicks, "progressTicks", 0L);
        }
    }
}

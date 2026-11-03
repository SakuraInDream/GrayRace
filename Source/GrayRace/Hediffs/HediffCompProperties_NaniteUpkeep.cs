using Verse;

namespace SD.GrayRace.Hediffs;

/// <summary>
/// 持续消耗备用灰潮源质来维持一个 Hediff 效果。
/// </summary>
public class HediffCompProperties_NaniteUpkeep : HediffCompProperties
{
    /// <summary>每秒消耗的备用灰潮源质。</summary>
    public float naniteCostPerSecond = 2f;

    /// <summary>扣费结算间隔（tick）。默认 60 = 1 秒。</summary>
    public int tickInterval = 60;

    /// <summary>资源不足时是否自动移除该 Hediff。</summary>
    public bool removeWhenExhausted = true;

    public HediffCompProperties_NaniteUpkeep()
    {
        compClass = typeof(HediffComp_NaniteUpkeep);
    }
}

using Verse;

namespace SD.GrayRace.DefModExtensions
{
    /// <summary>
    /// 纳米变形器的产物输出位置配置。
    /// 偏移量相对建筑中心计算，不随建筑旋转，便于按固定贴图的传送带位置调整。
    /// </summary>
    public class DefModExtension_NaniteTransmuterOutput : DefModExtension
    {
        public IntVec3 outputOffset = IntVec3.Zero;
    }
}

using Verse;

namespace SD.GrayRace.DefModExtensions
{
    /// <summary>
    /// 纳米重塑台的产物输出位置配置。
    /// 偏移量相对建筑中心计算，并随建筑旋转；默认对准贴图上的传送带。
    /// </summary>
    public class DefModExtension_NaniteReconfiguratorOutput : DefModExtension
    {
        public IntVec3 outputOffset = IntVec3.Zero;
    }
}

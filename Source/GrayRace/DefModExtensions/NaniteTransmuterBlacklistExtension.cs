using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.DefModExtensions
{
    /// <summary>
    /// 挂在 GR_NaniteTransmuter 的 ThingDef 上，集中配置禁止自动生成的资源。
    /// </summary>
    public class DefModExtension_NaniteTransmuterBlacklist : DefModExtension
    {
        public List<ThingDef> blacklistedDefs;
    }
}

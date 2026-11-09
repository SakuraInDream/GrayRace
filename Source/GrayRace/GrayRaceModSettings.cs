using Verse;

namespace SD.GrayRace
{
    /// <summary>
    /// 全局 Mod 配置。
    /// 每存档的状态（如设计器窗口几何）不放这里，见
    /// <see cref="SD.GrayRace.ThingClasses.Building_GR_Drydock"/> 上的存档字段。
    /// </summary>
    public class GrayRaceModSettings: ModSettings
    {
        /// <summary>
        /// 是否屏蔽 Gray 的非心情强制精神状态（默认开启）。
        /// </summary>
        public bool enableMentalStateImmunity = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableMentalStateImmunity, "enableMentalStateImmunity", true);
        }
    }
}

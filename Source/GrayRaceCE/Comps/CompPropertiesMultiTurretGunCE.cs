using SD.GrayRace.Comps;

namespace SD.GrayRace.CE.Comps;

/// <summary>
/// CE 专用多硬点炮塔 CompProperties。
/// 仅在 1.7/CE 的 XML 补丁中使用，CE 未启用时不会加载这个程序集。
/// </summary>
public class CompPropertiesMultiTurretGunCE : CompPropertiesMultiTurretGun
{
    public CompPropertiesMultiTurretGunCE()
    {
        compClass = typeof(CompMultiTurretGunCE);
    }
}

using Verse;

namespace SD.GrayRace.DefModExtensions;

/// <summary>
/// 挂在种族 ThingDef 上的标记：该种族的 Pawn 移动时忽略地形减速。
/// 挂在抽象基类上即可被所有子种族继承（例如 GR_Mech）。
/// </summary>
public class TerrainSkipExtension : DefModExtension
{
}

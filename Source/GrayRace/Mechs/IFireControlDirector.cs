using Verse;

namespace SD.GrayRace.Mechs;

/// <summary>
/// 多炮塔的目标分配接口。
/// 实现负责集中处理硬点目标分配和 AI 代表性武器选择。
/// </summary>
public interface IFireControlDirector
{
    /// <summary>在配装重建时预分配目标分配热路径需要的缓冲。</summary>
    void Prepare(int hardpointCapacity);

    /// <summary>为本轮可接收命令的硬点批量分配攻击目标。</summary>
    void AssignTargets(
        MechHardpoint[] hardpoints,
        int hardpointCount,
        Pawn owner,
        LocalTargetInfo forcedTarget,
        bool fireAtWill,
        LocalTargetInfo[] results);

    /// <summary>为 AI 走位决策选择"代表性"武器（决定寻路距离）。</summary>
    Verb SelectTacticalVerb(
        MechHardpoint[] hardpoints,
        int count,
        Thing target);
}

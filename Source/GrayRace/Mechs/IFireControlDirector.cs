using Verse;

namespace SD.GrayRace.Mechs;

/// <summary>
/// 火控策略接口。由 GRMechCombatComputerModuleDef.fireControlClass 驱动。
/// 不同战斗计算机模块可以实现不同的火控逻辑（齐射/轮射/集火/分散），
/// 通过 XML 配置即可切换，无需新增 C# 代码。
/// </summary>
public interface IFireControlDirector
{
    /// <summary>为一个空闲的硬点分配攻击目标。</summary>
    /// <returns>true 表示分配了目标，硬点应进入 WarmingUp 状态。</returns>
    bool TryAssignTarget(
        MechHardpoint hardpoint,
        Pawn owner,
        LocalTargetInfo forcedTarget,
        bool fireAtWill,
        System.Collections.Generic.HashSet<Thing> reservedTargets,
        out LocalTargetInfo target);

    /// <summary>为 AI 走位决策选择"代表性"武器（决定寻路距离）。</summary>
    Verb SelectTacticalVerb(
        MechHardpoint[] hardpoints,
        int count,
        Thing target);
}

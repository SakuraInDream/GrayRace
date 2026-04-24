using Verse;
using Verse.AI;

namespace SD.GrayRace.Mechs;

/// <summary>
/// 可复用 IAttackTargetSearcher 适配器。
/// 将 MechHardpoint 的 Verb 适配为 AttackTargetFinder.BestShootTargetFromCurrentPosition 需要的接口。
/// 必须是 class：原版 API 接收接口参数，struct 会在热路径被装箱。
/// </summary>
internal sealed class HardpointSearcher : IAttackTargetSearcher
{
    private MechHardpoint hp;
    private Pawn owner;

    public void Configure(MechHardpoint hardpoint, Pawn pawn)
    {
        hp = hardpoint;
        owner = pawn;
    }

    public Thing Thing => owner;

    public Verb CurrentEffectiveVerb => hp?.CurrentEffectiveVerb;

    public LocalTargetInfo LastAttackedTarget => hp.lastAttackedTarget;

    public int LastAttackTargetTick => hp.lastAttackTargetTick;
}

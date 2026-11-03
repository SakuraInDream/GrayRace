using RimWorld;
using SD.GrayRace.Modules;
using Verse;

namespace SD.GrayRace.Hediffs;

/// <summary>
/// 按固定速率持续扣除 NaniteModule 中的备用灰潮源质；
/// 资源不足（或不存在 NaniteModule）时自动结束该 Hediff。
/// </summary>
public class HediffComp_NaniteUpkeep : HediffComp
{
    private NaniteModule _nanites;
    private int _ticksSinceCharge;

    public HediffCompProperties_NaniteUpkeep Props => (HediffCompProperties_NaniteUpkeep)props;

    private NaniteModule Nanites => _nanites ??= Pawn.GetManager()?.naniteModule;

    public override void CompPostMake()
    {
        base.CompPostMake();
        _nanites = Pawn.GetManager()?.naniteModule;
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        base.CompPostTickInterval(ref severityAdjustment, delta);

        if (Pawn.Dead)
        {
            return;
        }

        int interval = Props.tickInterval > 0 ? Props.tickInterval : 60;
        _ticksSinceCharge += delta;
        if (_ticksSinceCharge < interval)
        {
            return;
        }

        int chargeCount = _ticksSinceCharge / interval;
        _ticksSinceCharge -= chargeCount * interval;

        float cost = Props.naniteCostPerSecond * chargeCount * interval / 60f;
        if (cost <= 0f)
        {
            return;
        }

        NaniteModule module = Nanites;
        if (module == null || module.CurrentNanites + 0.0001f < cost)
        {
            if (Props.removeWhenExhausted)
            {
                Stop(module == null ? "GR_NaniteUpkeepNoModule" : "GR_NaniteUpkeepExhausted");
            }

            return;
        }

        module.OffsetNanites(0f - cost);
    }

    private void Stop(string messageKey)
    {
        if (!messageKey.NullOrEmpty() && Pawn.Faction == Faction.OfPlayer)
        {
            Messages.Message(messageKey.Translate(Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.NegativeEvent, false);
        }

        // ShouldRemove => Severity <= 0，由 HealthTickInterval 安全移除
        if (parent.Severity > 0f)
        {
            parent.Severity = 0f;
        }
        else
        {
            Pawn.health.RemoveHediff(parent);
        }
    }
}

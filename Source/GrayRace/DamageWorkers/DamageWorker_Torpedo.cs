using RimWorld;
using SD.GrayRace.DefModExtensions;
using UnityEngine;
using Verse;

namespace SD.GrayRace.DamageWorkers;

// 鱼雷伤害：按命中目标的 BodySize 放大伤害，穿甲不变。
// 另外我必须吐槽一句，P社搞的这个算法是基础伤害*1+目标舰容，除非目标是0舰容，不然打不出标伤。
// 在环世界估计很超模，打个老鼠都能打几十伤害，稍微大点直接上百。
public class DamageWorker_Torpedo : DamageWorker_AddInjury
{
    public override DamageResult Apply(DamageInfo dinfo, Thing victim)
    {
        if (victim is Pawn pawn)
        {
            TorpedoDamageExtension extension = def.GetModExtension<TorpedoDamageExtension>();
            if (extension != null)
            {
                float factor = extension.GetDamageFactor(pawn.BodySize);
                if (!Mathf.Approximately(factor, 1f))
                {
                    dinfo.SetAmount(dinfo.Amount * factor);
                }
            }
        }

        return base.Apply(dinfo, victim);
    }
}

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SD.GrayRace.DefModExtensions;

//   <modExtensions>
//     <li Class="SD.GrayRace.DefModExtensions.TorpedoDamageExtension">
//       <damagePerBodySize>1</damagePerBodySize>
//       <maxDamageFactor>10</maxDamageFactor>
//     </li>
//   </modExtensions>
//
// 伤害倍率 = 1 + BodySize × damagePerBodySize
public class TorpedoDamageExtension : DefModExtension
{
    // 每 1.0 体型额外增加的伤害倍率（1 体型 = 1 + 1 倍）
    public float damagePerBodySize = 1f;

    // 伤害倍率上限
    public float maxDamageFactor = 10f;

    public float GetDamageFactor(float bodySize)
    {
        float factor = 1f + Mathf.Max(0f, bodySize) * damagePerBodySize;
        return Mathf.Clamp(factor, 1f, maxDamageFactor);
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (damagePerBodySize < 0f)
        {
            yield return "damagePerBodySize must be >= 0.";
        }

        if (maxDamageFactor < 1f)
        {
            yield return "maxDamageFactor must be >= 1.";
        }
    }
}

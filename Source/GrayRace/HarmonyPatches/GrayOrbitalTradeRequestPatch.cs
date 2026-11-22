using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

/// <summary>
/// 把 Gray_Faction 通讯菜单里的“请求轨道贸易商”改成“请求贸易”：
/// 按下后直接打开交易窗口，货单取派系的 baseTraderKinds，每次请求都重新生成。
/// 仍然沿用原版商船交易的判定：
///  - 必须有通电的轨道交易信标，否则提示 MessageNeedBeaconToTradeWithShip；
///  - 买入的物品由 TradeShip.GiveSoldThingToPlayer 空投到信标附近（DropCellFinder.TradeDropSpot）。
/// 不受原版“必须盟友 / -30 好感 / 15 天冷却”限制，只要求非敌对。
/// </summary>
[HarmonyPatch(typeof(FactionDialogMaker), "RequestOrbitalTraderOption")]
public static class GrayOrbitalTradeRequestPatch
{
    private const string GrayFactionDefName = "Gray_Faction";

    [HarmonyPostfix]
    public static void Postfix(Map map, Faction faction, Pawn negotiator, ref DiaOption __result)
    {
        if (__result == null || faction?.def == null || faction.def.defName != GrayFactionDefName)
        {
            return;
        }

        TraderKindDef traderKind = PickTraderKind(faction.def);
        if (traderKind == null)
        {
            // 没有任何可用的贸易种类，保留原版选项
            return;
        }

        DiaOption option = new DiaOption("GR_RequestTrade".Translate())
        {
            resolveTree = true
        };

        if (faction.HostileTo(Faction.OfPlayer))
        {
            option.Disable("GR_RequestTradeHostile".Translate());
        }
        else
        {
            option.action = delegate
            {
                // 与原版商船一致：必须有通电的轨道交易信标
                if (!Building_OrbitalTradeBeacon.AllPowered(map).Any())
                {
                    Messages.Message("MessageNeedBeaconToTradeWithShip".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                AcceptanceReport canTrade = negotiator.CanTradeWith(faction, traderKind);
                if (!canTrade.Accepted)
                {
                    if (!canTrade.Reason.NullOrEmpty())
                    {
                        Messages.Message(canTrade.Reason, MessageTypeDefOf.RejectInput, historical: false);
                    }
                    return;
                }

                // 临时商船：不加入 PassingShipManager，不占存档、不会自行离开
                TradeShip ship = new TradeShip(traderKind, faction)
                {
                    passingShipManager = map.passingShipManager
                };

                // 每次请求都重新生成货单
                ship.GenerateThings();

                Find.WindowStack.Add(new Dialog_Trade(negotiator, ship));
                LessonAutoActivator.TeachOpportunity(ConceptDefOf.BuildOrbitalTradeBeacon, OpportunityType.Critical);
            };
        }

        __result = option;
    }

    private static TraderKindDef PickTraderKind(FactionDef def)
    {
        if (!def.baseTraderKinds.NullOrEmpty())
        {
            return def.baseTraderKinds.RandomElement();
        }
        if (!def.orbitalTraderKinds.NullOrEmpty())
        {
            return def.orbitalTraderKinds.RandomElement();
        }
        if (!def.caravanTraderKinds.NullOrEmpty())
        {
            return def.caravanTraderKinds.RandomElement();
        }
        return null;
    }
}

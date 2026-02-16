using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Modules;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

/// <summary>
/// 机体升级页面
/// </summary>
public class Page_Upgrade : BasePageITab
{
    private bool _refreshRequested;
    private List<BodyPartRecord> _upgradeableParts = new List<BodyPartRecord>();
    private List<GRUpgradeDef> _availableUpgradesForSelectedPart = new List<GRUpgradeDef>();
    private BodyPartRecord _selectedPart;

    private UpgradeModule UpgradeComp => pawn?.GetManager()?.upgradeModule;

    public override string PageLabel => "升级";

    public override void OnPawnChanged()
    {
        _upgradeableParts.Clear();
        _availableUpgradesForSelectedPart.Clear();
        _selectedPart = null;

        if (pawn == null || UpgradeComp == null) return;

        _upgradeableParts = GetDisplayableBodyParts()
            .Where(part => UpgradeComp.HasUpgradeDefinitionForPart(part) || UpgradeComp.HasAnyActiveUpgrade(part))
            .ToList();

        SetDefaultSelection();
        RefreshAvailableUpgrades();
    }

    public override void DrawPartList(Rect rect)
    {
        DrawPartListGeneric(
            rect,
            _upgradeableParts,
            _selectedPart,
            part =>
            {
                _selectedPart = part;
                RefreshAvailableUpgrades();
            },
            GetPartLabel,
            GetPartLabelColor
        );
    }

    public override void DrawDetailPanel(Rect rect)
    {
        if (_selectedPart == null)
        {
            Widgets.Label(rect, "请先在左侧选择一个身体部位。".Colorize(Color.gray));
            return;
        }

        if (UpgradeComp == null)
        {
            Widgets.Label(rect, "升级模块不可用。".Colorize(ColorLibrary.RedReadable));
            return;
        }

        Listing_Standard listing = new Listing_Standard();
        listing.Begin(rect);

        listing.Label($"<b>当前部位:</b> {_selectedPart.LabelCap}");
        listing.GapLine();

        if (_availableUpgradesForSelectedPart.Count == 0)
        {
            listing.Label("该部位无可用升级方案。".Colorize(Color.gray));
        }
        else
        {
            foreach (GRUpgradeDef upgradeDef in _availableUpgradesForSelectedPart)
            {
                DrawUpgradeOption(listing, upgradeDef);
            }
        }

        listing.End();

        if (_refreshRequested)
        {
            _refreshRequested = false;
            RefreshAvailableUpgrades();
        }
    }

    public override string GetPageTitle()
    {
        return "机体升级";
    }

    private void SetDefaultSelection()
    {
        if (_selectedPart == null || !_upgradeableParts.Contains(_selectedPart))
        {
            _selectedPart = _upgradeableParts.FirstOrDefault();
        }
    }

    private void RefreshAvailableUpgrades()
    {
        if (_selectedPart == null || UpgradeComp == null)
        {
            _availableUpgradesForSelectedPart = new List<GRUpgradeDef>();
            return;
        }

        _availableUpgradesForSelectedPart = UpgradeComp.GetAvailableUpgradesForPart(_selectedPart);
    }

    private string GetPartLabel(BodyPartRecord part)
    {
        bool hasActiveUpgrade = UpgradeComp != null && UpgradeComp.HasAnyActiveUpgrade(part);

        string prefix = hasActiveUpgrade ? "★" : "○";
        return $"{prefix} {part.LabelCap}";
    }

    private Color GetPartLabelColor(BodyPartRecord part)
    {
        bool hasActiveUpgrade = UpgradeComp != null && UpgradeComp.HasAnyActiveUpgrade(part);
        return hasActiveUpgrade ? Color.green : Color.white;
    }

    private void DrawUpgradeOption(Listing_Standard listing, GRUpgradeDef def)
    {
        bool isActive = UpgradeComp != null && UpgradeComp.IsUpgradeActive(def, _selectedPart);
        string reason = string.Empty;
        bool canApply = isActive || (UpgradeComp != null && UpgradeComp.CanApplyUpgrade(def, _selectedPart, out reason));

        Rect rowRect = listing.GetRect(90f);
        Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
        Widgets.DrawHighlightIfMouseover(rowRect);

        Rect iconRect = new Rect(rowRect.x + 5f, rowRect.y + 5f, 45f, 45f);
        Widgets.DrawTextureFitted(iconRect, BaseContent.PlaceholderImage, 1f);

        Rect textRect = new Rect(iconRect.xMax + 10f, rowRect.y + 2f, rowRect.width - 170f, rowRect.height - 4f);
        Rect labelRect = new Rect(textRect.x, textRect.y, textRect.width, 20f);
        Rect descRect = new Rect(textRect.x, labelRect.yMax + 1f, textRect.width, 20f);
        Rect extraRect = new Rect(textRect.x, descRect.yMax + 1f, textRect.width, 40f);

        string typeLabel = def.upgradeType == UpgradeType.Transformation ? "[变形]" : "[插件]";
        Color typeColor = def.upgradeType == UpgradeType.Transformation ? Color.cyan : Color.magenta;

        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, $"{typeLabel.Colorize(typeColor)} {def.LabelCap}");

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(descRect, (def.description ?? string.Empty).Truncate(textRect.width));

        GUI.color = Color.white;
        DrawUpgradeExtraInfo(extraRect, def, canApply, reason, isActive);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        Rect btnRect = new Rect(rowRect.xMax - 95f, rowRect.y + 30f, 90f, 28f);
        string buttonLabel = isActive ? "停用" : (def.upgradeType == UpgradeType.Plugin ? "安装" : "启用");

        if (Widgets.ButtonText(btnRect, buttonLabel))
        {
            TryToggleUpgrade(def);
        }

        if (!canApply && !isActive && !reason.NullOrEmpty())
        {
            TooltipHandler.TipRegion(rowRect, reason);
        }

        listing.Gap(5f);
    }

    private void DrawUpgradeExtraInfo(Rect rect, GRUpgradeDef def, bool canApply, string reason, bool isActive)
    {
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Tiny;

        string researchText = def.researchPrerequisite == null
            ? "科技: 无前置"
            : (def.researchPrerequisite.IsFinished
                ? $"科技: {def.researchPrerequisite.LabelCap} (已解锁)"
                : $"科技: {def.researchPrerequisite.LabelCap} (未解锁)");

        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), researchText);

        string requirementText = def.upgradeType == UpgradeType.Plugin
            ? (UpgradeComp?.BuildPluginRequirementSummary(def) ?? "需求: 无")
            : "变形无需材料消耗";
        Widgets.Label(new Rect(rect.x, rect.y + 16f, rect.width, 18f), requirementText.Truncate(rect.width));

        if (!isActive && !canApply && !reason.NullOrEmpty())
        {
            GUI.color = ColorLibrary.RedReadable;
            Widgets.Label(new Rect(rect.x, rect.y + 30f, rect.width, 18f), reason.Truncate(rect.width));
            GUI.color = Color.white;
        }
        else if (isActive)
        {
            GUI.color = Color.green;
            Widgets.Label(new Rect(rect.x, rect.y + 30f, rect.width, 18f), "当前已启用");
            GUI.color = Color.white;
        }
    }

    private void TryToggleUpgrade(GRUpgradeDef def)
    {
        if (_selectedPart == null || pawn == null || def == null || UpgradeComp == null) return;

        bool success = UpgradeComp.TryToggleUpgrade(def, _selectedPart, out string feedback, out MessageTypeDef msgType);

        if (!feedback.NullOrEmpty())
        {
            Messages.Message(feedback, pawn, msgType);
        }

        if (success)
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            _refreshRequested = true; // 不要这里直接 RefreshAvailableUpgrades()
        }
        else
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }
    }
}

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
        Texture2D icon = def.uiIcon;
        if (icon == null || icon == BaseContent.BadTex)
        {
            icon = BaseContent.PlaceholderImage;
        }
        Widgets.DrawTextureFitted(iconRect, icon, 1f);

        Rect textRect = new Rect(iconRect.xMax + 10f, rowRect.y + 2f, rowRect.width - 170f, rowRect.height - 4f);
        Rect labelRect = new Rect(textRect.x, textRect.y, textRect.width, 20f);
        Rect descRect = new Rect(textRect.x, labelRect.yMax + 1f, textRect.width, 20f);
        Rect extraRect = new Rect(textRect.x, descRect.yMax + 1f, textRect.width, 40f);

        string typeLabel = def.IsTransformation ? "[变形]" : "[插件]";
        Color typeColor = def.IsTransformation ? Color.cyan : Color.magenta;

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
        string buttonLabel = isActive ? "停用" : (def.IsPlugin ? "安装" : "启用");

        if (Widgets.ButtonText(btnRect, buttonLabel))
        {
            TryToggleUpgrade(def);
        }

        TooltipHandler.TipRegion(rowRect, BuildUpgradeTooltip(def, isActive, canApply, reason));

        listing.Gap(5f);
    }

    private void DrawUpgradeExtraInfo(Rect rect, GRUpgradeDef def, bool canApply, string reason, bool isActive)
    {
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Tiny;

        List<ResearchProjectDef> researchProjects = def.EnumerateResearchPrerequisites().ToList();
        string researchText;
        if (researchProjects.Count == 0)
        {
            researchText = "科技: 无前置";
        }
        else
        {
            int finishedCount = researchProjects.Count(project => project.IsFinished);
            string statusText = finishedCount == researchProjects.Count
                ? "已解锁"
                : $"未解锁 {researchProjects.Count - finishedCount}/{researchProjects.Count}";

            researchText = $"科技: {researchProjects.Select(project => project.LabelCap.ToString()).ToCommaList()} ({statusText})";
        }

        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), researchText);

        string requirementText = def.IsPlugin
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
            _refreshRequested = true;
        }
        else
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }
    }

    private string BuildUpgradeTooltip(GRUpgradeDef def, bool isActive, bool canApply, string reason)
    {
        if (def == null) return string.Empty;

        string effectText = BuildEffectTooltipText(def);

        return $"{def.LabelCap}\n{def.description}\n\n{effectText}";
    }

    private string BuildEffectTooltipText(GRUpgradeDef def)
    {
        HediffDef hediffDef = def?.hediffToApply;
        if (hediffDef == null) return "效果: 无";

        List<string> lines = new List<string>();

        if (hediffDef.addedPartProps != null)
        {
            float partEfficiency = hediffDef.addedPartProps.partEfficiency;
            if (Mathf.Abs(partEfficiency - 1f) > 0.0001f)
            {
                lines.Add($"部位效率: {FormatPercentDelta(partEfficiency - 1f)}");
            }
        }

        if (hediffDef.stages != null && hediffDef.stages.Count > 0)
        {
            bool multiStage = hediffDef.stages.Count > 1;

            for (int i = 0; i < hediffDef.stages.Count; i++)
            {
                HediffStage stage = hediffDef.stages[i];
                if (stage == null) continue;

                string stagePrefix = multiStage ? $"阶段{i + 1} " : string.Empty;
                AppendStatOffsets(lines, stage.statOffsets, stagePrefix);
                AppendStatFactors(lines, stage.statFactors, stagePrefix);
            }
        }

        if (lines.Count == 0) return "效果: 无明确数值加成";
        return "效果:\n" + string.Join("\n", lines);
    }

    private static void AppendStatOffsets(List<string> output, List<StatModifier> modifiers, string prefix)
    {
        if (output == null || modifiers == null || modifiers.Count == 0) return;

        foreach (StatModifier modifier in modifiers)
        {
            if (modifier?.stat == null || Mathf.Abs(modifier.value) <= 0.0001f) continue;

            string valueText = modifier.value.ToStringByStyle(modifier.stat.toStringStyle);
            if (modifier.value > 0f && !valueText.StartsWith("+"))
            {
                valueText = "+" + valueText;
            }

            output.Add($"{prefix}{modifier.stat.LabelCap}: {valueText}");
        }
    }

    private static void AppendStatFactors(List<string> output, List<StatModifier> modifiers, string prefix)
    {
        if (output == null || modifiers == null || modifiers.Count == 0) return;

        foreach (StatModifier modifier in modifiers)
        {
            if (modifier?.stat == null || Mathf.Abs(modifier.value - 1f) <= 0.0001f) continue;

            output.Add($"{prefix}{modifier.stat.LabelCap}: x{modifier.value:0.##} ({FormatPercentDelta(modifier.value - 1f)})");
        }
    }

    private static string FormatPercentDelta(float delta)
    {
        float percent = delta * 100f;
        string sign = percent >= 0f ? "+" : string.Empty;
        return $"{sign}{percent:0.#}%";
    }
}

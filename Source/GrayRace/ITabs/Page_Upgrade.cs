using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Modules;
using SD.GrayRace.Utilities;
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
    private readonly List<BodyPartRecord> _upgradeableParts = new List<BodyPartRecord>();
    private readonly HashSet<BodyPartRecord> _upgradeablePartSet = new HashSet<BodyPartRecord>();
    private readonly List<GRUpgradeDef> _availableUpgradesForSelectedPart = new List<GRUpgradeDef>();
    private BodyPartRecord _selectedPart;
    private Vector2 _detailScrollPosition = Vector2.zero;
    private float _detailViewHeight;

    private UpgradeModule UpgradeComp => pawn?.GetManager()?.upgradeModule;

    public override string PageLabel => "升级";

    public override void OnPawnChanged()
    {
        _upgradeableParts.Clear();
        _upgradeablePartSet.Clear();
        _availableUpgradesForSelectedPart.Clear();
        _selectedPart = null;
        _detailScrollPosition = Vector2.zero;
        _detailViewHeight = 0f;

        if (pawn == null || UpgradeComp == null) return;

        RebuildUpgradeableParts();

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
                _detailScrollPosition = Vector2.zero;
                _detailViewHeight = 0f;
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

        Rect outRect = rect;
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(_detailViewHeight, outRect.height));

        Widgets.BeginScrollView(outRect, ref _detailScrollPosition, viewRect);

        Listing_Standard listing = new Listing_Standard();
        listing.maxOneColumn = true;
        listing.Begin(new Rect(0f, 0f, viewRect.width, viewRect.height));

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

        if (Event.current.type == EventType.Layout)
        {
            _detailViewHeight = Mathf.Max(listing.CurHeight + 6f, outRect.height);
        }

        Widgets.EndScrollView();

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
            _availableUpgradesForSelectedPart.Clear();
            return;
        }

        _availableUpgradesForSelectedPart.Clear();
        _availableUpgradesForSelectedPart.AddRange(UpgradeComp.GetAvailableUpgradesForPart(_selectedPart));
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

        string typeLabel = def.IsTransformation ? "[变形]" : "[插件]";
        Color typeColor = def.IsTransformation ? Color.cyan : Color.magenta;
        Color warningColor = new Color(1f, 0.72f, 0.2f);
        string labelName = def.LabelCap.ToString();

        string titleSuffix = string.Empty;
        if (!isActive && UpgradeComp != null)
        {
            string replaceHint = UpgradeComp.GetReplacementHint(def, _selectedPart);
            if (!replaceHint.NullOrEmpty())
            {
                titleSuffix = " " + replaceHint;
            }
        }

        string labelText = typeLabel + " " + labelName + titleSuffix;
        string titleSuffixColored = titleSuffix.NullOrEmpty() ? string.Empty : titleSuffix.Colorize(warningColor);
        string descText = def.description ?? string.Empty;

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

        string requirementText = def.IsPlugin
            ? (UpgradeComp?.BuildPluginRequirementSummary(def) ?? "需求: 无")
            : "变形无需材料消耗";

        bool showStateLine = false;
        string stateText = string.Empty;
        Color stateColor = Color.white;
        if (!isActive && !canApply && !reason.NullOrEmpty())
        {
            showStateLine = true;
            stateText = reason;
            stateColor = ColorLibrary.RedReadable;
        }
        else if (isActive)
        {
            showStateLine = true;
            stateText = "当前已启用";
            stateColor = Color.green;
        }

        bool prevWordWrap = Text.WordWrap;
        Text.WordWrap = true;

        float rowWidth = listing.ColumnWidth;
        float textWidth = rowWidth - 170f;

        Text.Font = GameFont.Small;
        float labelHeight = Mathf.Max(20f, Text.CalcHeight(labelText, textWidth));

        Text.Font = GameFont.Tiny;
        float descHeight = Mathf.Max(20f, Text.CalcHeight(descText, textWidth));
        float researchHeight = Mathf.Max(18f, Text.CalcHeight(researchText, textWidth));
        float requirementHeight = Mathf.Max(18f, Text.CalcHeight(requirementText, textWidth));
        float stateHeight = showStateLine ? Mathf.Max(18f, Text.CalcHeight(stateText, textWidth)) : 0f;

        float lineGap = 2f;
        float extraHeight = researchHeight + lineGap + requirementHeight + (showStateLine ? lineGap + stateHeight : 0f);
        float contentHeight = labelHeight + 1f + descHeight + 1f + extraHeight;
        float rowHeight = Mathf.Max(90f, contentHeight + 4f);

        Rect rowRect = listing.GetRect(rowHeight);
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
        Rect labelRect = new Rect(textRect.x, textRect.y, textRect.width, labelHeight);
        Rect descRect = new Rect(textRect.x, labelRect.yMax + 1f, textRect.width, descHeight);
        Rect extraRect = new Rect(textRect.x, descRect.yMax + 1f, textRect.width, extraHeight);

        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(labelRect, $"{typeLabel.Colorize(typeColor)} {labelName}{titleSuffixColored}");

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(descRect, descText);

        GUI.color = Color.white;
        DrawUpgradeExtraInfo(
            extraRect,
            researchText,
            researchHeight,
            requirementText,
            requirementHeight,
            stateText,
            stateHeight,
            showStateLine,
            stateColor,
            lineGap
        );

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

        Text.WordWrap = prevWordWrap;
    }

    private void DrawUpgradeExtraInfo(
        Rect rect,
        string researchText,
        float researchHeight,
        string requirementText,
        float requirementHeight,
        string stateText,
        float stateHeight,
        bool showStateLine,
        Color stateColor,
        float lineGap
    )
    {
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Tiny;

        float y = rect.y;
        Widgets.Label(new Rect(rect.x, y, rect.width, researchHeight), researchText);
        y += researchHeight + lineGap;

        Widgets.Label(new Rect(rect.x, y, rect.width, requirementHeight), requirementText);
        y += requirementHeight + lineGap;

        if (showStateLine && !stateText.NullOrEmpty())
        {
            GUI.color = stateColor;
            Widgets.Label(new Rect(rect.x, y, rect.width, stateHeight), stateText);
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

    private void RebuildUpgradeableParts()
    {
        _upgradeableParts.Clear();
        _upgradeablePartSet.Clear();

        List<BodyPartRecord> allParts = pawn?.RaceProps?.body?.AllParts;
        if (allParts == null || allParts.Count == 0) return;

        List<GRUpgradeDef> allDefs = DefDatabase<GRUpgradeDef>.AllDefsListForReading;
        for (int i = 0; i < allDefs.Count; i++)
        {
            GRUpgradeDef def = allDefs[i];
            if (!IsValidUpgradeDef(def)) continue;

            for (int j = 0; j < allParts.Count; j++)
            {
                BodyPartRecord part = allParts[j];
                if (part == null) continue;

                if (UpgradeTargetMatcher.Matches(def, part) && _upgradeablePartSet.Add(part))
                {
                    _upgradeableParts.Add(part);
                }
            }
        }

        if (UpgradeComp != null)
        {
            for (int j = 0; j < allParts.Count; j++)
            {
                BodyPartRecord part = allParts[j];
                if (part == null) continue;

                if (UpgradeComp.HasAnyActiveUpgrade(part) && _upgradeablePartSet.Add(part))
                {
                    _upgradeableParts.Add(part);
                }
            }
        }

        _upgradeableParts.Sort(CompareParts);
    }

    private static bool IsValidUpgradeDef(GRUpgradeDef def)
    {
        return def != null
               && def.hediffToApply != null
               && UpgradeTargetMatcher.HasAnyTargetRule(def);
    }

    private static float GetListPriority(BodyPartRecord rec)
    {
        if (rec == null) return 9999999f;
        return ((int)rec.height * 10000) + rec.coverageAbsWithChildren;
    }

    private static int CompareParts(BodyPartRecord a, BodyPartRecord b)
    {
        float pa = GetListPriority(a);
        float pb = GetListPriority(b);

        if (pa > pb) return -1;
        if (pa < pb) return 1;

        string la = a?.LabelCap ?? string.Empty;
        string lb = b?.LabelCap ?? string.Empty;
        return string.CompareOrdinal(la, lb);
    }
}

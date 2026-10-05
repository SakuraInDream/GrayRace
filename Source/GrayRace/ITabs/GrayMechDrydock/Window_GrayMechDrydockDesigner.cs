using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class Window_GrayMechDrydockDesigner : Window
{
    /// <summary>
    /// 原始设计尺寸。三栏比例就是按这个尺寸推出来的；画布本身会跟随窗口伸缩。
    /// </summary>
    private static readonly Vector2 DesignCanvasSize = GrayMechDrydockTabStyle.WindowSize;

    /// <summary>
    /// 画布最多能缩到设计尺寸的多少倍。
    /// 低于这个下限就不再压缩了，改为保持该尺寸并平移——因为再小下去
    /// 各面板 <c>ContractedBy(8f)</c> 会把矩形挤成负尺寸，内层
    /// <c>Widgets.BeginScrollView</c> 会抛异常，而 Window.InnerWindowOnGUI
    /// 是 catch 住后每帧打 Log.Error 的，会刷日志。
    /// 0.5 时画布为 890x410：左右栏各 168、中栏 464，各面板仍为正尺寸且可用。
    /// </summary>
    private const float MinCanvasScale = 0.5f;

    /// <summary>画布最小尺寸。视口小于它时才启用平移滚动条。</summary>
    private static readonly Vector2 MinCanvasSize = DesignCanvasSize * MinCanvasScale;

    /// <summary>
    /// 三栏可分配的主体宽度（原始设计下算出来）：
    /// 设计宽 - 左右外边距 - 关闭按钮预留 - 2 个栏间距 = 1780 - 10*2 - 22 - 12*2 = 1714
    /// </summary>
    private static readonly float BaseBodyWidth = DesignCanvasSize.x
        - GrayMechDrydockTabStyle.Margin * 2f
        - GrayMechDrydockTabStyle.CloseButtonReserveRight
        - GrayMechDrydockTabStyle.PanelGap * 2f;

    /// <summary>左栏比例 = 360 / 1714 ≈ 21.0%，取自原始设计的左栏宽度。</summary>
    private static readonly float LeftColumnRatio = GrayMechDrydockTabStyle.LeftPanelWidth / BaseBodyWidth;

    /// <summary>右栏比例 = 360 / 1714 ≈ 21.0%，取自原始设计的右栏宽度。</summary>
    private static readonly float RightColumnRatio = GrayMechDrydockTabStyle.RightPanelWidth / BaseBodyWidth;

    /// <summary>底部主按钮栏高度。与 MainTabWindow 一致，避免窗口压住它。</summary>
    private const float BottomBarReserve = 35f;

    private const float PanBarSize = 16f;
    private const float CursorOffset = 14f;
    private const float CursorIconSize = 32f;

    private readonly Building_GR_Drydock dock;
    private readonly GrayMechDrydockViewState viewState = new();
    private readonly GrayMechDrydockPresenter state = new();
    private readonly GrayMechDrydockTabController controller;
    private readonly GrayMechDrydockDesignerPanel designerPanel = new();
    private readonly GrayMechDrydockFocusPanel focusPanel = new();
    private readonly GrayMechDrydockBottomBarPanel bottomBarPanel = new();

    private Vector2 canvasPan = Vector2.zero;

    // 大肥鱼这两处代码太弱智了，后面再人工强制改一次
    private Rect rememberedRect;
    private bool geometrySavePending;

    public Window_GrayMechDrydockDesigner(Building_GR_Drydock dock)
    {
        this.dock = dock;
        controller = new GrayMechDrydockTabController(state);

        doWindowBackground = true;
        doCloseX = true;
        doCloseButton = false;
        draggable = true;
        resizeable = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
    }

    public override Vector2 InitialSize
    {
        get
        {
            Vector2 desired = DesignCanvasSize + new Vector2(Margin * 2f, Margin * 2f);
            GrayRaceModSettings settings = GrayRaceMod.Settings;
            if (settings != null)
            {
                desired = new Vector2(settings.designerWindowWidth, settings.designerWindowHeight);
            }

            desired.x = Mathf.Min(Mathf.Max(desired.x, MinResizeSize.x), UI.screenWidth);
            desired.y = Mathf.Min(Mathf.Max(desired.y, MinResizeSize.y), UI.screenHeight - BottomBarReserve);
            return desired;
        }
    }

    private static readonly Vector2 MinResizeSize = new(150f, 150f);

    protected override void SetInitialSizeAndPosition()
    {
        GrayRaceModSettings settings = GrayRaceMod.Settings;
        if (settings == null)
        {
            base.SetInitialSizeAndPosition();
            rememberedRect = windowRect;
            return;
        }

        Vector2 size = InitialSize;
        float x = Mathf.Clamp(settings.designerWindowX, 0f, Mathf.Max(0f, UI.screenWidth - size.x));
        float y = Mathf.Clamp(settings.designerWindowY, 0f, Mathf.Max(0f, (float)UI.screenHeight - size.y));
        windowRect = new Rect(x, y, size.x, size.y).Rounded();
        rememberedRect = windowRect;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        rememberedRect = windowRect;
    }

    public override void PostClose()
    {
        RememberGeometryIfChanged();
        base.PostClose();
    }

    protected override void LateWindowOnGUI(Rect inRect)
    {
        if (geometrySavePending)
        {
            geometrySavePending = false;
            RememberGeometryIfChanged();
            return;
        }

        if (Event.current.type == EventType.MouseUp)
        {
            geometrySavePending = true;
        }
    }

    private void RememberGeometryIfChanged()
    {
        if (Mathf.Abs(windowRect.x - rememberedRect.x) < 1f
            && Mathf.Abs(windowRect.y - rememberedRect.y) < 1f
            && Mathf.Abs(windowRect.width - rememberedRect.width) < 1f
            && Mathf.Abs(windowRect.height - rememberedRect.height) < 1f)
        {
            return;
        }

        GrayRaceModSettings settings = GrayRaceMod.Settings;
        if (settings == null)
        {
            return;
        }

        rememberedRect = windowRect;
        settings.SetDesignerWindowGeometry(windowRect.x, windowRect.y, windowRect.width, windowRect.height);

        settings.Write();
    }

    internal static void ResetGeometry()
    {
        GrayRaceModSettings settings = GrayRaceMod.Settings;
        if (settings != null)
        {
            settings.ClearDesignerWindowGeometry();
            settings.Write();
        }

        IList<Window> openWindows = Find.WindowStack.Windows;
        for (int i = 0; i < openWindows.Count; i++)
        {
            if (openWindows[i] is Window_GrayMechDrydockDesigner designer)
            {
                // SetInitialSizeAndPosition 末尾会同步 rememberedRect，所以不会又被写回去。
                designer.SetInitialSizeAndPosition();
            }
        }
    }

    /// <summary>设计器入口。同一个船坞已经开着时不重复打开。</summary>
    internal static void OpenFor(Building_GR_Drydock dock)
    {
        if (dock == null)
        {
            return;
        }

        IList<Window> openWindows = Find.WindowStack.Windows;
        for (int i = 0; i < openWindows.Count; i++)
        {
            if (openWindows[i] is Window_GrayMechDrydockDesigner existing && existing.dock == dock)
            {
                return;
            }
        }

        // WindowStack.Add 会先移除同类型窗口，所以切换船坞时旧窗口会被替换掉。
        Find.WindowStack.Add(new Window_GrayMechDrydockDesigner(dock));
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (dock == null || dock.Destroyed || !dock.Spawned)
        {
            Close();
            return;
        }

        state.EnsureCaches(dock, viewState);
        GrayMechDrydockTabContext context = new(dock, state, viewState, controller);

        ResolveViewport(inRect, out Rect viewport, out bool panX, out bool panY);
        float maxPanX = Mathf.Max(0f, MinCanvasSize.x - viewport.width);
        float maxPanY = Mathf.Max(0f, MinCanvasSize.y - viewport.height);
        canvasPan.x = panX ? Mathf.Clamp(canvasPan.x, 0f, maxPanX) : 0f;
        canvasPan.y = panY ? Mathf.Clamp(canvasPan.y, 0f, maxPanY) : 0f;

        // 画布整体平移：用裁剪组 + 平移绘制，而不是外层再套一个 GUI.BeginScrollView。
        // Unity IMGUI 的嵌套滚动视图会互相抢滚轮事件（内层会失效），而画布内部
        // 本来就已经有 4 个滚动视图（区段画布 / 模块列表 / 右侧汇总 / 底部设计库）。
        // 这里所有矩形都按 viewport 局部坐标算，Mouse.IsOver 才能对得上。
        //
        // 画布默认就等于视口——窗口放大、缩小都跟着走，三栏按比例重分。
        // 只有视口缩到 MinCanvasSize 以下才停下并平移（见 MinCanvasScale 的说明）。
        Vector2 canvasSize = new(
            panX ? MinCanvasSize.x : viewport.width,
            panY ? MinCanvasSize.y : viewport.height);
        Rect canvasRect = new(
            panX ? -canvasPan.x : 0f,
            panY ? -canvasPan.y : 0f,
            canvasSize.x,
            canvasSize.y);
        Widgets.BeginGroup(viewport);
        DrawCanvas(context, canvasRect);
        Widgets.EndGroup();

        if (panX)
        {
            // 只有画布停在下限尺寸时才会走到这里，所以滚动范围就是下限宽。
            canvasPan.x = GUI.HorizontalScrollbar(
                new Rect(inRect.x, viewport.yMax, viewport.width, PanBarSize),
                canvasPan.x,
                viewport.width,
                0f,
                MinCanvasSize.x);
        }

        if (panY)
        {
            canvasPan.y = GUI.VerticalScrollbar(
                new Rect(viewport.xMax, inRect.y, PanBarSize, viewport.height),
                canvasPan.y,
                viewport.height,
                0f,
                MinCanvasSize.y);
        }

        // 光标图标画在裁剪组之外：跟随鼠标即可，不受画布平移影响。
        DrawArmedModuleCursor(viewport);
    }

    /// <summary>
    /// 由 <paramref name="inRect"/> 算出真正用来绘制画布的视口，以及两个方向是否需要滚动条。
    /// 这里比较的是 <see cref="MinCanvasSize"/>（下限）而不是设计尺寸——视口比下限大时
    /// 画布直接等于视口，两个方向都不会有滚动条。
    ///
    /// 迭代两轮：滚动条自身要占掉对向 16px，所以第一轮的结论会改变第二轮的可判空间。
    /// 只判一轮的话会出现「画布比 inRect 窄、但比扣掉滚动条后的视口宽」——右边被裁掉
    /// 一条却没有任何横向滚动条可以拉。两轮即可收敛：对向滚动条只可能从无到有，
    /// 不会从有到无，所以第二轮的结果就是最终结果。
    /// </summary>
    private static void ResolveViewport(Rect inRect, out Rect viewport, out bool panX, out bool panY)
    {
        float availWidth = inRect.width;
        float availHeight = inRect.height;
        panX = false;
        panY = false;
        for (int i = 0; i < 2; i++)
        {
            panX = MinCanvasSize.x > availWidth;
            panY = MinCanvasSize.y > availHeight;
            availWidth = inRect.width - (panY ? PanBarSize : 0f);
            availHeight = inRect.height - (panX ? PanBarSize : 0f);
        }

        viewport = new Rect(inRect.x, inRect.y, Mathf.Max(1f, availWidth), Mathf.Max(1f, availHeight));
    }

    /// <summary>
    /// 画布排版。原来是从 ITab_GrayMechDrydock.FillTab 原样搬过来的，现在三栏
    /// （左选择栏 / 中区段画布 / 右汇总栏）按固定比例分配宽度，所以窗口放大或缩小后
    /// 三栏的占比都不变。栏间距与页边距仍然固定（不参与比例，缩放时保持视觉一致），
    /// 因此在非设计尺寸下三栏占整窗的比例会有零点几个百分点的偏差，这是有意的。
    ///
    /// 传入设计尺寸（1780）时，算出的各矩形与原实现完全一致：
    /// 左 360 / 中 994 / 右 360 / 主体 1366。
    /// 画布宽度有 <see cref="MinCanvasSize"/> 兜底，缩到底时中栏仍有 464。
    /// </summary>
    private void DrawCanvas(GrayMechDrydockTabContext context, Rect canvas)
    {
        Rect root = new(
            canvas.x + GrayMechDrydockTabStyle.Margin,
            canvas.y + GrayMechDrydockTabStyle.Margin + GrayMechDrydockTabStyle.CloseButtonReserveTop,
            canvas.width - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveRight,
            canvas.height - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveTop);

        float bodyWidth = Mathf.Max(0f, root.width - GrayMechDrydockTabStyle.PanelGap * 2f);
        float leftPanelWidth = bodyWidth * LeftColumnRatio;
        float rightPanelWidth = bodyWidth * RightColumnRatio;
        float centerPanelWidth = Mathf.Max(0f, bodyWidth - leftPanelWidth - rightPanelWidth);
        float mainBodyWidth = leftPanelWidth + GrayMechDrydockTabStyle.PanelGap + centerPanelWidth;

        Rect mainBodyRect = new(root.x, root.y, mainBodyWidth, root.height);
        Rect summaryRect = new(
            mainBodyRect.xMax + GrayMechDrydockTabStyle.PanelGap,
            root.y,
            rightPanelWidth,
            root.height);

        float bottomBarHeight = state.GetBottomBarHeight(mainBodyWidth);
        float centerHeight = Mathf.Max(120f, mainBodyRect.height - bottomBarHeight - GrayMechDrydockTabStyle.PanelGap);
        Rect upperRect = new(mainBodyRect.x, mainBodyRect.y, mainBodyWidth, centerHeight);
        Rect leftRect = new(upperRect.x, upperRect.y, leftPanelWidth, upperRect.height);
        Rect centerRect = new(
            leftRect.xMax + GrayMechDrydockTabStyle.PanelGap,
            upperRect.y,
            centerPanelWidth,
            upperRect.height);
        Rect bottomRect = new(
            mainBodyRect.x,
            upperRect.yMax + GrayMechDrydockTabStyle.PanelGap,
            mainBodyWidth,
            bottomBarHeight);

        designerPanel.Draw(context, centerRect);
        focusPanel.DrawSelection(context, leftRect);
        focusPanel.DrawSummary(context, summaryRect);
        bottomBarPanel.Draw(context, bottomRect);
    }

    private void DrawArmedModuleCursor(Rect visibleRect)
    {
        GRMechModuleDef armedModule = state.ArmedModule;
        if (armedModule == null)
        {
            return;
        }

        Vector2 mousePosition = Event.current.mousePosition;
        if (!visibleRect.Contains(mousePosition))
        {
            return;
        }

        Rect iconRect = new(mousePosition.x + CursorOffset, mousePosition.y + CursorOffset, CursorIconSize, CursorIconSize);
        iconRect.x = Mathf.Min(iconRect.x, visibleRect.xMax - iconRect.width - 4f);
        iconRect.y = Mathf.Min(iconRect.y, visibleRect.yMax - iconRect.height - 4f);

        Texture2D icon = GrayMechDrydockTabStyle.GetModuleIcon(armedModule);
        if (icon != null)
        {
            Widgets.DrawTextureFitted(iconRect, icon, 1f);
        }
        else if (armedModule.equipmentDef != null)
        {
            Widgets.ThingIcon(iconRect, armedModule.equipmentDef, armedModule.equipmentStuff);
        }
    }
}

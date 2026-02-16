# 灰裔种族 Manager Comp 模式可行性分析报告

## 1. 概述
本报告旨在分析将 `CompResource_Nanites`、`CompOverclock` 等多个 `ThingComp` 整合为单一 `CompGrayRaceManager` 的可行性。

## 2. 核心方案：外观模式 (Facade Pattern)
通过将分散的 `ThingComp` 重构为由主 `CompGrayRaceManager` 管理的**子系统 (Sub-systems)**，可以实现 XML 配置的整洁化和逻辑的统一管理。

### 架构对比

**当前架构 (Current):**
- XML:
  - `CompProperties_Nanites`
  - `CompProperties_Overclock`
  - ...
- C#:
  - `pawn.GetComp<CompResource_Nanites>()`
  - `pawn.GetComp<CompOverclock>()`

**建议架构 (Proposed):**
- XML:
  - `CompProperties_GrayRaceManager` (单一入口)
- C#:
  - `pawn.GetComp<CompGrayRaceManager>().Nanites`
  - `pawn.GetComp<CompGrayRaceManager>().Overclock`

## 3. 实施细节

### 3.1 代码结构
定义一个抽象基类 `GrayRaceSystem`，所有功能模块继承此类而非 `ThingComp`。

```csharp
// 基类
public abstract class GrayRaceSystem : IExposable
{
    protected Pawn pawn;
    protected CompGrayRaceManager manager;

    public GrayRaceSystem(CompGrayRaceManager manager, Pawn pawn)
    {
        this.manager = manager;
        this.pawn = pawn;
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad) { }
    public virtual void CompTick() { }
    public virtual void ExposeData() { }
}

// Manager
public class CompGrayRaceManager : ThingComp
{
    public GrayRaceSystem_Nanites Nanites;
    public GrayRaceSystem_Overclock Overclock;
    public GrayRaceSystem_Upgrades Upgrades;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (Nanites == null) Nanites = new GrayRaceSystem_Nanites(this, parent as Pawn);
        if (Overclock == null) Overclock = new GrayRaceSystem_Overclock(this, parent as Pawn);

        Nanites.PostSpawnSetup(respawningAfterLoad);
        Overclock.PostSpawnSetup(respawningAfterLoad);
    }

    // ... Tick 和 ExposeData 类似转发 ...
}
```

### 3.2 引用重构
当前代码库中约有 **30 处** 需要修改的引用。
- 主要涉及文件：`Gizmo_NaniteResources.cs`, `StatPart_OverclockDrain.cs`, `Need_GrayRaceEnergy.cs`, `HediffComp_NanitesRegeneration.cs` 等。
- 修改示例：
  `var comp = pawn.TryGetComp<CompResource_Nanites>();`
  变为：
  `var comp = pawn.GetGrayRaceManager()?.Nanites;` (建议封装扩展方法)

## 4. 风险评估

### 4.1 存档兼容性 (高风险)
- **问题**：直接移除 XML 中的旧 `CompProperties` 会导致旧存档加载时丢失对应数据（如当前纳米值、超频状态）。RimWorld 加载存档依赖 `def.comps` 列表。
- **缓解**：如果项目处于开发阶段，建议接受坏档（重新开档）。如果必须兼容，需要保留旧代码并编写复杂的迁移逻辑（不推荐）。

### 4.2 工作量 (中等)
- 代码结构调整：约 2-3 小时。
- 引用替换与测试：约 1-2 小时。

## 5. 结论
方案在技术上完全可行，且能显著改善代码结构和 XML 可读性。建议在接受“坏档”的前提下进行重构。

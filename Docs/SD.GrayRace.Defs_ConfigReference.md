# SD.GrayRace.Defs 配置参考

本文档记录当前源码中 `SD.GrayRace.Defs` 命名空间下的自定义 Def，以及编写这些 Def 时必须使用的嵌套配置类型和枚举。内容以当前工作树中的 C# 实现和 `1.6/Defs/SD.GrayRace.Defs.*` XML 为准。

不在本文范围内：原版/HAR Def、`DefModExtension`、Comp、工具类，以及只存在于设计方案而尚未实现的类型（例如 `GRMechTargetScoringDef`）。

## 1. 配置顺序与通用规则

推荐按下面的依赖顺序定义：

1. `GRMechSlotSizeDef`、`GRMechComponentSetDef`
2. `GRMechSlotDef`
3. `GRMechSectionSlotDef`
4. `GRMechChassisDef`
5. `GRMechSectionLayoutDef`
6. `GRMechModuleDef` 及其派生类型
7. `GRUpgradeDef`

所有具体 Def 都继承 `Verse.Def`，通常可以使用以下通用字段：

| XML 字段 | 类型 | 要求 | 说明 |
| --- | --- | --- | --- |
| `defName` | `string` | 具体 Def 必需 | 全局稳定标识。抽象父模板可以只写 `Name="..." Abstract="True"`。 |
| `label` | `string` | 推荐 | 游戏内显示名称。 |
| `description` | `string` | 推荐 | 游戏内说明。 |

除非字段章节另有说明，引用其他 Def 时填写目标 Def 的 `defName`；列表使用 `<li>`；`ThingDefCountClass` 可以使用 `<ThingDefName>数量</ThingDefName>`。

## 2. 枚举与嵌套类型

### 2.1 枚举

#### `GRMechSlotComponentType`

XML 可用值：`Weapon`、`StrikeCraft`、`Utility`、`Auxiliary`。`Undefined` 是未配置状态，不应写入有效 Def。

#### `GRMechCoreComponentRole`

XML 可用值：`PowerCore`、`Thruster`、`Sensor`。`Undefined` 无效。

#### `GRMechWeaponMountMode`

| 值 | 说明 |
| --- | --- |
| `Hardpoint` | 默认值。武器由 `CompMultiTurretGun` 建立为独立浮游炮硬点。 |
| `PrimaryEquipment` | 作为 Pawn 的主武器处理；只能用于 `Weapon` 槽。 |

#### `GRMechSlotCategory`

值为 `Weapon`、`Utility`、`Auxiliary`、`CoreSystem`、`Undefined`。这是由 `GRMechSlotDef` 的 `requiredComponentSet` 或 `componentType` 计算出的只读分类，不是 XML 字段。

### 2.2 `GRMechSlotEntry`

该类型不是 Def，用在 `GRMechChassisDef.requiredComponentSlots` 和 `GRMechSectionLayoutDef.slots` 的 `<li>` 中。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `key` | `string` | `null` | 必需；必须在所属列表内唯一，也是设计存档使用的稳定槽位键。 |
| `label` | `string` | `null` | 可选显示名。 |
| `slotDef` | `GRMechSlotDef` | `null` | 必需，决定组件类型、尺寸、类别、颜色与图标。 |
| `weaponMountMode` | `GRMechWeaponMountMode` | `Hardpoint` | 仅武器槽可用 `PrimaryEquipment`。 |
| `anchorBodyPart` | `BodyPartDef` | `null` | 可选，指定槽位对应的身体部位。 |
| `hardpointAnchor` | `Vector2?` | 缺失 | Hardpoint 浮游炮相对 Pawn 中心的屏幕水平、垂直锚点，单位为格；参与绘制、暖机扇形和实际弹丸起点。 |
| `uiOrder` | `int` | `0` | UI 排序，越小越靠前。 |

`Hardpoint + Weapon` 必须配置 `hardpointAnchor`；`PrimaryEquipment` 和非武器槽禁止配置。`(0,0)` 是合法的 Pawn 中心锚点，只有节点缺失才表示未配置。锚点完全由 XML 决定，运行时只会在该锚点周围执行轻微游动和移动滞后，不会改变锚点或生成额外布局坐标，也不会按槽位顺序生成回退坐标。射程判定使用浮游炮当前 `anchorPosition` 转换出的整数格；LOS、掩体和命中率仍使用 Pawn 中心及原版射击报告。

```xml
<li>
  <key>SMALL_GUN_01</key>
  <label>一号浮游炮</label>
  <slotDef>GR_MechSlot_SmallTurret</slotDef>
  <weaponMountMode>Hardpoint</weaponMountMode>
  <anchorBodyPart>MechanicalArm</anchorBodyPart>
  <hardpointAnchor>(-0.5,0.2)</hardpointAnchor>
  <uiOrder>10</uiOrder>
</li>
```

## 3. 槽位基础 Def

### 3.1 `GRMechSlotSizeDef`

XML 根标签：`SD.GrayRace.Defs.GRMechSlotSizeDef`

定义 S/M/L/X/T 等槽位尺寸以及允许承载的组件类型。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `stellarisSizeKey` | `string` | `null` | 必需，不翻译的稳定尺寸键。 |
| `glyph` | `string` | `null` | 必需，设计器内显示的短字符。 |
| `uiOrder` | `int` | `0` | UI 排序。 |
| `allowedComponentTypes` | `List<GRMechSlotComponentType>` | 空列表 | 必须至少包含一个非 `Undefined` 值，不可重复。 |

源码中的 `Allows()` 对空列表会视为“允许所有已定义类型”，但 `ConfigErrors()` 同时会把空列表报告为错误，因此有效配置必须显式填写。

```xml
<SD.GrayRace.Defs.GRMechSlotSizeDef>
  <defName>GR_MechSlotSize_Small</defName>
  <label>small</label>
  <stellarisSizeKey>small</stellarisSizeKey>
  <glyph>S</glyph>
  <uiOrder>100</uiOrder>
  <allowedComponentTypes>
    <li>Weapon</li>
    <li>Utility</li>
  </allowedComponentTypes>
</SD.GrayRace.Defs.GRMechSlotSizeDef>
```

### 3.2 `GRMechComponentSetDef`

XML 根标签：`SD.GrayRace.Defs.GRMechComponentSetDef`

定义不使用尺寸的核心组件集合。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `coreRole` | `GRMechCoreComponentRole` | `Undefined` | 必须配置为有效核心角色。 |
| `glyph` | `string` | `null` | 必需，核心槽默认显示字符。 |
| `uiOrder` | `int` | `0` | UI 排序。 |

```xml
<SD.GrayRace.Defs.GRMechComponentSetDef>
  <defName>GR_MechComponentSet_PowerCore</defName>
  <label>power core</label>
  <coreRole>PowerCore</coreRole>
  <glyph>R</glyph>
  <uiOrder>100</uiOrder>
</SD.GrayRace.Defs.GRMechComponentSetDef>
```

### 3.3 `GRMechSlotDef`

XML 根标签：`SD.GrayRace.Defs.GRMechSlotDef`

定义模块实际匹配的槽位。一个槽位必须在“普通尺寸槽”和“核心组件槽”之间二选一。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `componentType` | `GRMechSlotComponentType` | `Undefined` | 普通槽必需；核心槽禁止配置。 |
| `requiredComponentSet` | `GRMechComponentSetDef` | `null` | 核心槽必需；普通槽禁止配置。 |
| `slotSize` | `GRMechSlotSizeDef` | `null` | 普通槽必需，且尺寸必须允许 `componentType`；核心槽必须留空。 |
| `isFixed` | `bool` | `false` | 标记固定槽，例如轴基武器槽。 |
| `designerColor` | `Color` | `(1,1,1,1)` | 设计器显示颜色。 |
| `glyph` | `string` | `null` | 可选覆盖字符；为空时依次使用组件集或尺寸的 `glyph`。 |

普通武器槽示例：

```xml
<SD.GrayRace.Defs.GRMechSlotDef>
  <defName>GR_MechSlot_SmallTurret</defName>
  <label>small turret slot</label>
  <componentType>Weapon</componentType>
  <slotSize>GR_MechSlotSize_Small</slotSize>
  <designerColor>(0.8,0.42,0.16)</designerColor>
</SD.GrayRace.Defs.GRMechSlotDef>
```

核心槽示例：

```xml
<SD.GrayRace.Defs.GRMechSlotDef>
  <defName>GR_MechSlot_PowerCore</defName>
  <label>反应堆</label>
  <requiredComponentSet>GR_MechComponentSet_PowerCore</requiredComponentSet>
  <designerColor>(0.46,0.86,0.79)</designerColor>
</SD.GrayRace.Defs.GRMechSlotDef>
```

### 3.4 `GRMechSectionSlotDef`

XML 根标签：`SD.GrayRace.Defs.GRMechSectionSlotDef`

定义船体可拥有的区段位置，如舰艏、核心和舰艉。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `slotId` | `string` | `null` | 必需，不翻译的稳定位置键；当前常用 `bow`、`mid`、`stern`。 |
| `uiOrder` | `int` | `0` | UI 排序。 |

```xml
<SD.GrayRace.Defs.GRMechSectionSlotDef>
  <defName>GR_MechSection_Bow</defName>
  <label>舰艏区段</label>
  <slotId>bow</slotId>
  <uiOrder>100</uiOrder>
</SD.GrayRace.Defs.GRMechSectionSlotDef>
```

## 4. 机体结构 Def

### 4.1 `GRMechChassisDef`

XML 根标签：`SD.GrayRace.Defs.GRMechChassisDef`

定义机械体类型、固定制造时间、可选区段位置和必装核心槽。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `pawnKindDef` | `PawnKindDef` | `null` | 必需，且其 `race` 必须存在；完成制造时使用。 |
| `sectionSlots` | `List<GRMechSectionSlotDef>` | 空列表 | 至少一个，不可为 `null` 或重复。 |
| `requiredComponentSlots` | `List<GRMechSlotEntry>` | 空列表 | 至少一个；每项的 `key` 唯一、`slotDef` 必须属于 `CoreSystem` 且具有有效 `coreRole`。 |
| `fixedWorkTicks` | `int` | `60000` | 固定制造总时长，必须大于 `0`，不随组件变化。 |
| `researchPrerequisites` | `List<ResearchProjectDef>` | 空列表 | 全部完成后才可用；重复项在枚举时去重。 |
| `hardpointSwarmSettings` | `GRMechHardpointSwarmSettings` | 见下表 | 可选的 chassis 级浮游炮蜂群手感配置；整个节点可省略。 |
| `designerPreviewPath` | `string` | `null` | 设计器中的机体预览贴图路径。 |
| `uiOrder` | `int` | `0` | UI 排序。 |

`GRMechHardpointSwarmSettings`：

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `wanderRadius` | `float` | `0.08` | 锚点周围的最大轻微游动范围，单位为格，必须 `>= 0`。 |
| `movementLag` | `float` | `0.25` | Pawn 移动时的无人机式滞后强度，必须在 `0..1`。 |

推荐把通用 `requiredComponentSlots` 放在抽象父模板中：

```xml
<SD.GrayRace.Defs.GRMechChassisDef Name="GR_ChassisBase" Abstract="True">
  <requiredComponentSlots>
    <li>
      <key>POWER_CORE_01</key>
      <label>反应堆</label>
      <slotDef>GR_MechSlot_PowerCore</slotDef>
      <anchorBodyPart>Reactor</anchorBodyPart>
      <uiOrder>10</uiOrder>
    </li>
  </requiredComponentSlots>
</SD.GrayRace.Defs.GRMechChassisDef>

<SD.GrayRace.Defs.GRMechChassisDef ParentName="GR_ChassisBase">
  <defName>GR_Chassis_Corvette</defName>
  <label>护卫舰</label>
  <pawnKindDef>GR_Mech_FF</pawnKindDef>
  <fixedWorkTicks>6000</fixedWorkTicks>
  <designerPreviewPath>Race/Mech/FF_south</designerPreviewPath>
  <hardpointSwarmSettings>
    <wanderRadius>0.08</wanderRadius>
    <movementLag>0.25</movementLag>
  </hardpointSwarmSettings>
  <researchPrerequisites>
    <li>GR_T1MechTech</li>
  </researchPrerequisites>
  <sectionSlots>
    <li>GR_MechSection_Mid</li>
  </sectionSlots>
  <uiOrder>100</uiOrder>
</SD.GrayRace.Defs.GRMechChassisDef>
```

### 4.2 `GRMechSectionLayoutDef`

XML 根标签：`SD.GrayRace.Defs.GRMechSectionLayoutDef`

定义某种船体在某个区段位置可选择的具体布局及其槽位组合。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `shipSize` | `GRMechChassisDef` | `null` | 必需。字段名保留自当前源码，实际含义是所属船体。 |
| `sectionSlot` | `GRMechSectionSlotDef` | `null` | 必需，且必须存在于 `shipSize.sectionSlots`。 |
| `slots` | `List<GRMechSlotEntry>` | 空列表 | 至少一个；`key` 唯一；这里只允许非核心槽，并要求槽位具有尺寸。 |
| `costList` | `List<ThingDefCountClass>` | 空列表 | 选择该布局增加的制造材料。 |
| `researchPrerequisites` | `List<ResearchProjectDef>` | 空列表 | 布局解锁研究。 |
| `uiOrder` | `int` | `0` | UI 排序。 |

```xml
<SD.GrayRace.Defs.GRMechSectionLayoutDef>
  <defName>GR_Corvette_MID_S3</defName>
  <label>拦截者核心</label>
  <shipSize>GR_Chassis_Corvette</shipSize>
  <sectionSlot>GR_MechSection_Mid</sectionSlot>
  <costList>
    <GR_Alloy>40</GR_Alloy>
  </costList>
  <researchPrerequisites>
    <li>GR_T1MechTech</li>
  </researchPrerequisites>
  <slots>
    <li>
      <key>SMALL_GUN_01</key>
      <slotDef>GR_MechSlot_SmallTurret</slotDef>
      <hardpointAnchor>(-0.5,0.2)</hardpointAnchor>
      <uiOrder>0</uiOrder>
    </li>
    <li>
      <key>SMALL_UTILITY_01</key>
      <slotDef>GR_MechSlot_SmallUtility</slotDef>
      <uiOrder>10</uiOrder>
    </li>
  </slots>
  <uiOrder>400</uiOrder>
</SD.GrayRace.Defs.GRMechSectionLayoutDef>
```

## 5. 模块 Def

### 5.1 `GRMechModuleDef`

XML 根标签：`SD.GrayRace.Defs.GRMechModuleDef`

所有普通模块和核心模块的基类。模块与槽位通过 `compatibleSlots` 直接匹配。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `compatibleSlots` | `List<GRMechSlotDef>` | 空列表 | 至少一个，不可重复；所有槽必须属于同一个 `GRMechSlotCategory`。 |
| `allowedChassis` | `List<GRMechChassisDef>` | 空列表 | 可选白名单；为空表示不限制船体，不可重复。 |
| `upgradesTo` | `List<GRMechModuleDef>` | 空列表 | 可升级目标；不可为自身或重复，目标必须拥有相同的 `compatibleSlots` 集合。 |
| `power` | `int` | `0` | 净功率：正值供能，负值耗能，零值不影响预算。 |
| `costList` | `List<ThingDefCountClass>` | 空列表 | 模块制造成本。 |
| `researchPrerequisites` | `List<ResearchProjectDef>` | 空列表 | 解锁研究；重复项在枚举时去重。 |
| `researchUnlockGroup` | `string` | `null` | 可选研究解锁展示组；同一非空组必须恰好有一个代表项。 |
| `isResearchUnlockRepresentative` | `bool` | `false` | 标记研究面板中代表该组显示的 Def；不改变模块自身的研究条件。 |
| `iconPath` | `string` | `null` | UI 图标资源路径；加载失败时保持默认坏图。 |
| `slotFamily` | `string` | `null` | 同系列不同尺寸模块的稳定族名，用于武装模块自动寻找对应槽位版本。 |
| `equipmentDef` | `ThingDef` | `null` | 武器模块的内部装备。浮游炮要求它能产生 `CompEquippable` 和带弹丸的 Verb。 |
| `equipmentStuff` | `ThingDef` | `null` | 可选装备 Stuff；填写时必须同时有 `equipmentDef`，且目标必须是 Stuff。为空时使用装备的默认 Stuff。 |
| `weaponDeploymentTicks` | `int` | `0` | 有 `equipmentDef` 时必须大于 `0`，控制浮游炮展开/收回时间。 |
| `statOffsets` | `List<StatModifier>` | 空列表 | 装配后对 Pawn Stat 做加法修正。 |
| `statFactors` | `List<StatModifier>` | 空列表 | 装配后对 Pawn Stat 做乘法修正。 |
| `anchorBodyPart` | `BodyPartDef` | `null` | 模块对应的身体部位锚点。 |
| `uiOrder` | `int` | `0` | UI 排序。 |

武器命中率和强制偏射不在模块 Def 中重复配置，统一读取 `equipmentDef` 的原版武器 Stat 与 `VerbProperties`。

武器模块示例：

```xml
<SD.GrayRace.Defs.GRMechModuleDef>
  <defName>GR_MechModule_PulseLaser_Infrared</defName>
  <label>红色激光</label>
  <compatibleSlots>
    <li>GR_MechSlot_SmallTurret</li>
  </compatibleSlots>
  <slotFamily>PulseLaser_Infrared</slotFamily>
  <power>-5</power>
  <costList>
    <GR_Alloy>10</GR_Alloy>
  </costList>
  <equipmentDef>GR_MechWeapon_PulseLaser_Infrared</equipmentDef>
  <weaponDeploymentTicks>30</weaponDeploymentTicks>
  <uiOrder>1100</uiOrder>
</SD.GrayRace.Defs.GRMechModuleDef>
```

非武器模块可以省略 `equipmentDef`、`equipmentStuff` 和 `weaponDeploymentTicks`。

### 5.2 `GRMechPowerCoreModuleDef`

XML 根标签：`SD.GrayRace.Defs.GRMechPowerCoreModuleDef`

继承 `GRMechModuleDef` 的全部字段，不增加动态船体档案。每个具体反应堆 Def 必须直接配置正数 `power`、非空 `costList` 和至少一个 `allowedChassis`。雷击舰与驱逐舰可在同一个 Def 的白名单中共享驱逐舰级反应堆；其余舰型应使用独立 Def。

同一科技等级可用抽象父 Def 复用名称、描述、图标、研究条件、研究解锁组和排序。具体 Def 只持有固定舰型数据与同舰型升级目标。研究面板只注入该组中 `isResearchUnlockRepresentative=true` 的 Def，但设计器仍按每个具体 Def 的 `researchPrerequisites` 和 `allowedChassis` 过滤。

```xml
<SD.GrayRace.Defs.GRMechPowerCoreModuleDef ParentName="GR_MechModule_ReactorTier_Fission">
  <defName>GR_MechModule_Reactor_Fission_Corvette</defName>
  <allowedChassis><li>GR_Chassis_Corvette</li></allowedChassis>
  <power>75</power>
  <costList><GR_Alloy>10</GR_Alloy></costList>
  <upgradesTo><li>GR_MechModule_Reactor_Fusion_Corvette</li></upgradesTo>
  <isResearchUnlockRepresentative>true</isResearchUnlockRepresentative>
</SD.GrayRace.Defs.GRMechPowerCoreModuleDef>
```

### 5.3 `GRMechThrusterModuleDef`

XML 根标签：`SD.GrayRace.Defs.GRMechThrusterModuleDef`

当前不增加字段或校验，全部配置继承自 `GRMechModuleDef`。使用推进器核心槽作为 `compatibleSlots`。

```xml
<SD.GrayRace.Defs.GRMechThrusterModuleDef>
  <defName>GR_MechModule_Thruster_Example</defName>
  <label>推进器示例</label>
  <compatibleSlots>
    <li>GR_MechSlot_Thruster</li>
  </compatibleSlots>
  <power>-10</power>
</SD.GrayRace.Defs.GRMechThrusterModuleDef>
```

该片段是类型模板；当前项目中的推进器实例可能仍使用 `GRMechModuleDef` 根标签。

### 5.4 `GRMechSensorModuleDef`

XML 根标签：`SD.GrayRace.Defs.GRMechSensorModuleDef`

当前不增加字段或校验，全部配置继承自 `GRMechModuleDef`。使用传感器核心槽作为 `compatibleSlots`。

```xml
<SD.GrayRace.Defs.GRMechSensorModuleDef>
  <defName>GR_MechModule_Sensor_Example</defName>
  <label>传感器示例</label>
  <compatibleSlots>
    <li>GR_MechSlot_Sensor</li>
  </compatibleSlots>
  <power>-10</power>
</SD.GrayRace.Defs.GRMechSensorModuleDef>
```

该片段是类型模板；当前传感器槽和实例尚未启用。

## 5. 升级 Def

### 5.1 `GRUpgradeDef`

XML 根标签：`SD.GrayRace.Defs.GRUpgradeDef`

同一个 Def 同时描述免费变形和需要材料安装的插件。`requiredMaterials` 有有效条目时是插件，否则是变形。

| XML 字段 | 类型 | 默认值 | 要求与作用 |
| --- | --- | --- | --- |
| `targetBodyPart` | `BodyPartDef` | `null` | 精确包含规则。 |
| `targetBodyPartTags` | `List<BodyPartTagDef>` | 空列表 | 标签包含规则。 |
| `targetBodyPartGroups` | `List<BodyPartGroupDef>` | 空列表 | 分组包含规则。 |
| `excludedBodyParts` | `List<BodyPartDef>` | 空列表 | 精确排除规则。 |
| `excludedBodyPartTags` | `List<BodyPartTagDef>` | 空列表 | 标签排除规则。 |
| `excludedBodyPartGroups` | `List<BodyPartGroupDef>` | 空列表 | 分组排除规则。 |
| `iconPath` | `string` | `null` | UI 图标资源路径。 |
| `hediffToApply` | `HediffDef` | `null` | 有效升级必需。 |
| `researchPrerequisites` | `List<ResearchProjectDef>` | 空列表 | 全部完成后显示/可用。 |
| `requiredMaterials` | `List<ThingDefCountClass>` | 空列表 | 非空时将升级分类为插件，并在安装时消耗材料。 |
| `workAmount` | `float` | `1000` | 插件安装工作量。 |
| `requiredInstallationBuilding` | `ThingDef` | `null` | 可选安装建筑；为空时允许原地安装。 |
| `requiredSkill` | `SkillDef` | `null` | 可选安装技能。 |
| `minSkillLevel` | `int` | `5` | 配置了 `requiredSkill` 时要求的最低等级。 |
| `allowOnAddedParts` | `bool` | `false` | 是否允许匹配 `Hediff_AddedPart` 产生的非原生部位。 |
| `uiOrder` | `int` | `0` | UI 排序。 |

目标匹配规则：

- 至少配置 `targetBodyPart`、`targetBodyPartTags`、`targetBodyPartGroups` 之一。
- 三种包含规则是“或”关系，没有优先级覆盖：命中任意一种即可进入候选。
- 任意排除规则命中都会否决该部位。
- 当前 `GRUpgradeDef` 没有覆写 `ConfigErrors()`；但 `UpgradeModule` 会把缺少 `hediffToApply` 或所有包含规则的 Def 视为无效并忽略。
- 插件与变形可以在同一部位共存；同类升级之间会互相替换。

插件示例：

```xml
<SD.GrayRace.Defs.GRUpgradeDef>
  <defName>GR_Plugin_Brain_CombatChip</defName>
  <label>战斗芯片插件</label>
  <targetBodyPart>Nano_Brain</targetBodyPart>
  <hediffToApply>GR_Hediff_CombatChip</hediffToApply>
  <researchPrerequisites>
    <li>MicroelectronicsBasics</li>
  </researchPrerequisites>
  <requiredMaterials>
    <ComponentIndustrial>5</ComponentIndustrial>
    <Plasteel>20</Plasteel>
  </requiredMaterials>
  <workAmount>2000</workAmount>
  <requiredSkill>Crafting</requiredSkill>
  <minSkillLevel>8</minSkillLevel>
  <uiOrder>10</uiOrder>
</SD.GrayRace.Defs.GRUpgradeDef>
```

变形示例：

```xml
<SD.GrayRace.Defs.GRUpgradeDef>
  <defName>GR_Transform_Vanilla_BionicEye</defName>
  <label>仿生眼形态</label>
  <targetBodyPart>SightSensor</targetBodyPart>
  <hediffToApply>BionicEye</hediffToApply>
  <researchPrerequisites>
    <li>Bionics</li>
  </researchPrerequisites>
  <uiOrder>80</uiOrder>
</SD.GrayRace.Defs.GRUpgradeDef>
```

## 6. 当前 11 个 Def 类型索引

| 类型 | 额外配置面 |
| --- | --- |
| `GRMechSlotSizeDef` | 槽位尺寸与允许的组件类型 |
| `GRMechComponentSetDef` | 核心组件角色集合 |
| `GRMechSlotDef` | 具体普通槽/核心槽 |
| `GRMechSectionSlotDef` | 舰艏/核心/舰艉等区段位置 |
| `GRMechChassisDef` | 船体、制造时间、区段位置和必装核心槽 |
| `GRMechSectionLayoutDef` | 船体某区段的槽位布局 |
| `GRMechModuleDef` | 通用模块基类 |
| `GRMechPowerCoreModuleDef` | 反应堆及按船体输出覆盖 |
| `GRMechThrusterModuleDef` | 推进器类型标识；无新增字段 |
| `GRMechSensorModuleDef` | 传感器类型标识；无新增字段 |
| `GRUpgradeDef` | 变形和插件升级 |

## 7. 源码与现有配置位置

- C# 定义：`Source/GrayRace/Defs/`
- XML 实例：`1.6/Defs/SD.GrayRace.Defs.*`
- 船体：`1.6/Defs/SD.GrayRace.Defs.GRMechDefs/`
- 区段布局：`1.6/Defs/SD.GrayRace.Defs.GRMechSectionLayoutDefs/`
- 模块：`1.6/Defs/SD.GrayRace.Defs.GRMechModuleDefs/`
- 升级：`1.6/Defs/SD.GrayRace.Defs.GRUpgradeDefs/`

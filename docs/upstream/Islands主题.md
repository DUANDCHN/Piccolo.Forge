# Islands 主题

> Rider 主题菜单里的 **Islands Dark / Islands Light / Islands Darcula**。
> 相对已复刻的 expUI（`Dark` / `Light`，方角、面板彼此贴死），Islands 把每块面板做成
> **圆角、四周留缝、浮在一片"海"上**的岛。
>
> 本文只审 Islands **相对 expUI 多出来的那部分**：几何、键、取值。
> 条带 / 停靠 / 标题条的模型本身不变，见 [工具窗口模型](工具窗口模型.md) 与 [度量与主题键](度量与主题键.md)。

上游位置：

| 路径 | 里面有什么 |
|---|---|
| `platform/platform-resources/src/themes/islands/` | `ManyIslandsDark/Light/Darcula.theme.json`、`IslandSchemeDark.xml` |
| `platform/platform-impl/src/com/intellij/openapi/application/impl/islands/` | `IslandsUICustomization.kt`（几何与画法）、`IslandsGradientPainter.kt`、`IslandsTabPainter.kt` |
| `platform/platform-impl/src/com/intellij/toolWindow/xNext/island/` | `XNextIslandHolder.kt`、`XNextRoundedBorder.kt` |
| `platform/platform-api/src/com/intellij/ui/IslandsState.kt` | 开关状态（`isEnabled` / `isCustomEnabled`） |
| `platform/platform-resources/src/themes/metadata/IntelliJPlatform.themeMetadata.json:1420-1462` | `Island.*` 键的权威清单，全部 `"since": "2026.1"` |

---

## 1. 核心：岛不是靠布局留缝，是**画完之后盖掉四条边**

这是全篇最要紧的一条，照着"给面板加 margin"去做会得到完全不同的结构。

出处 `IslandsUICustomization.kt:942-1029 · paintIslandAreaRaw`。面板照旧占满自己的矩形，
`paintAfterChildren` 里做三件事：

1. **盖边**（`:967-972`）——用 `getMainBackgroundColor()`（那片"海"的颜色）填四条边：

   ```
   offset = scale(Island.borderWidth / 2)        // 6/2 = 3
   fillRect(0, 0, width, offset)                 // 上
   fillRect(0, height-offset, width, offset)     // 下
   fillRect(0, offset, offset, height-2*offset)  // 左
   fillRect(width-offset, offset, offset, …)     // 右
   ```

2. **抠圆角**（`:978-1008`）——四个角各填一个二次贝塞尔围出的楔形，同样是海的颜色。
   控制点就是角点本身，`arcSizeF = scale(Island.arc / 2f)`，即 **`Island.arc` 是直径，圆角半径是它的一半**。

3. **描边**（`:1010-1028`，条件 `isIslandBorderLineNeeded`）——`Island.borderColor` 画四条直线 + 四段
   90° 圆弧。直线两端各让出 `Island.borderArcLength`（14），圆弧用 `arcSize = scale(Island.arc)` 作**直径**：

   ```
   drawArc(offset, offset, arcSize, arcSize, 90, 90)   // 左上，其余三角同构
   ```

内容不被这一圈盖住，靠的是面板自带的内边距（`:868`、`:880`）：

| 面板 | 键 | 兜底 |
|---|---|---|
| 工具窗口 | `Island.ToolWindow.insets` | `JBUI.insets(3)` |
| 编辑区 | `Island.Editor.insets` | `JBUI.insets(2)` |

> **`isIslandBorderLineNeeded`（`:933-937`）**：开渐变时恒 `true`；否则**设了编辑区背景图就不画描边**——
> 上游原话是描边压在背景图上「looks ugly」。

### 复刻时的取舍：Avalonia 用裁剪，不用"盖"

"画完再盖"是 Swing 的做法——子组件先画满整块，只能事后覆盖。Avalonia 里子控件受
`Clip` 约束，把岛裁成圆角矩形即可，**边上那一圈根本不会被画**，露出的就是父级的海色。
视觉结果与上游一致，少一次全尺寸重绘。

按第一原则，这是**有意的实现差异，不是数字差异**：`arc` / `borderWidth` / `borderArcLength` /
`insets` 全部照抄，只是把"覆盖"换成"裁剪"。

---

## 2. `Island.*` 键与取值

三份主题 JSON 里 `Island` 块**逐字节相同**（`ManyIslandsDark.theme.json:412-423`、
`ManyIslandsLight.theme.json:413-423`）：

| 键 | 常规 | 紧凑 | 代码兜底 | 含义 |
|---|---|---|---|---|
| `Island.arc` | 20 | 16 | 20（`:947`） | 圆角**直径**（半径 = 10） |
| `Island.borderArcLength` | 14 | 10 | 14（`:1013`） | 描边直线段两端让出的长度 |
| `Island.borderWidth` | 6 | 4 | 6（`:957`） | 缝的总宽；单边 = 它的一半 |
| `Island.borderColor` | `tool-window-bg` | — | `getMainBackgroundColor()` | 描边色 = **岛自己的底色** |
| `Island.inactiveAlpha` | 0.56 | — | 0.5（`:138`） | 窗口失活时岛的透明度 |
| `Island.toolWindowAlpha` | 0.2 | — | 0.2（`GradientPainter.kt:468`） | 渐变模式下工具窗口的透明度 |
| `Islands` | 1 | — | — | 该主题开启岛模式 |

> `Island.borderColor = tool-window-bg` 是**故意的**：描边与岛同色，等于给岛加一圈 1px 的自身延展，
> 把抗锯齿后的圆角边缘补实，而不是画一条看得见的框线。

---

## 3. 配色：岛与海

Islands 不是在 expUI 上改几个键，而是**另一套灰阶**（`gray-10`…`gray-160`），
`parentTheme` 是 `ExperimentalDark`。关键在于它引入了**两级底色**：

| 概念 | 键 | 谁用 |
|---|---|---|
| **岛** | `ToolWindow.background` ← `tool-window-bg` | 工具窗口、编辑区、标题条 |
| **海** | `MainWindow.background` ← `main-window-bg` | 主窗口、条带、状态栏、缝 |

⚠ **两套主题的别名指向不同的层，不能照搬**（`ManyIslandsDark:213,218` vs `ManyIslandsLight:213,218`）：

```
Dark  ： tool-window-bg = layer-0-bg   main-window-bg = layer-1-bg
Light ： tool-window-bg = layer-2-bg   main-window-bg = layer-0-bg
```

于是 **Dark 是"暗岛浮在稍亮的海上"，Light 是"白岛浮在灰海上"**——两边都是岛比海更"实"，
但走的层号相反。按 Dark 的映射去解 Light 会把黑白关系整个做反。

### 解析结果

沿 `别名 → 语义色 → 灰阶字面量` 解到底：

| Forge 字段 | 上游键 | Islands Dark | Islands Light |
|---|---|---|---|
| `Background`（岛） | `ToolWindow.background` | `#191A1C` (gray-10) | `#FFFFFF` (white) |
| `MainBackground`（海） | `MainWindow.background` | `#26282C` (gray-30) | `#E9EAEE` (gray-150) |
| `IslandBorder` | `Island.borderColor` | `#191A1C` | `#FFFFFF` |
| `StripeBackground` | `ToolWindow.Stripe.background` | `#26282C` | `#E9EAEE` |
| `StripeBorder` | `ToolWindow.Stripe.borderColor` | `transparent` | `transparent` |
| `WindowBorder` | `ToolWindow.borderColor` | `transparent` | `transparent` |
| `StripeSeparator` | `ToolWindow.Stripe.separatorColor` | `#FFFFFF21` | `#00000020` |
| `StripeSeparatorDragAndDrop` | `…Stripe.DragAndDrop.separatorColor` | `#3871E1` (blue-80) | `#3871E1` |
| `ButtonForeground` | `ToolWindow.Button.foreground` | `#9FA2A8` (gray-100) | `#5F6269` (gray-70) |
| `ButtonSelectedForeground` | `…Button.selectedForeground` | `#FFFFFF` | `#FFFFFF` |
| `ButtonSelectedBackground` | `…Button.selectedBackground` | `#3871E1` | `#3871E1` |
| `ButtonDropBackground` | `…Button.DragAndDrop.buttonDropBackground` | `#3671E580` | `#3671E580` |
| `ButtonDropBorder` | `…Button.DragAndDrop.buttonDropBorderColor` | `#3871E1` | `#3871E1` |
| `DragAreaBackground` | `ToolWindow.DragAndDrop.areaBackground` | `#366ACF4D` | `#366ACF4D` |
| `HeaderBackground` | `ToolWindow.Header.inactiveBackground` | `#191A1C` | `#FFFFFF` |
| `HeaderBorder` | `ToolWindow.Header.borderColor` | `#26282C` (layer-0-border) | `#E9EAEE` (layer-2-border) |
| `HeaderTabHoverBackground` | `…HeaderTab.hoverInactiveBackground` | `#FFFFFF17` | `#00000012` |
| `Foreground` | `*.foreground` ← `text-default` | `#D1D3D9` (gray-130) | `#000000` (black) |

> 颜色字面量仍是上游写法 `#RRGGBB[AA]`（alpha 在**最后**），与 Avalonia 相反，
> 解析走既有的 `ForgeColor.Parse`（见 [度量与主题键](度量与主题键.md) §6 的同一条坑）。

⚠ **`transparent` 是"别画"，不是"画一条透明线"**：`ToolWindow.borderColor` 与
`ToolWindow.Stripe.borderColor` 在 Islands 下都是透明——岛与海之间已经有缝，再画边框就是多一条线。
实现上取 `#00000000` 即可（这两处都是 1px 线，画一条 alpha=0 的线与不画视觉等价，
不值得为它把字段改成可空、连累三个既有调用点）。**要紧的是别顺手填 expUI 的边框色**——
那会在每个岛外面多描一圈，正是 Islands 要去掉的东西。

### 未纳入：`alt` 块

两份主题都有一个 `alt` 子块（`ManyIslandsDark:425-437`），把 `MainWindow.background` /
`ToolWindow.background` / `Island.borderColor` 换成 `*-alt` 色。它服务于
`UISettings.differentiateProjects`（`IslandsUICustomization.kt:180`，多项目开不同底色区分窗口）。
**单应用场景没有这个概念，不复刻。**

---

## 4. 度量：Islands 改了条带按钮

除 `Island.*` 外，Islands 主题还覆盖了一处既有度量（`ManyIslandsDark.theme.json:1190-1200`）：

键是**拼**出来的（`JBUI.java:1343`）：
`"StripeToolbar.Button." + (left|right) + "StripeIcon" + (WithName|"") + ".padding"`，
即 **左/右 × 显名/不显名 = 四份**。内边距字面量的顺序是 **`top,left,bottom,right`**。

| 键 | expUI（`darcula.theme.json:836-850` + 代码兜底） | Islands（`ManyIslandsDark:1191-1205`） |
|---|---|---|
| `size` | `40,40`（兜底） | **`37,40`** |
| `size.compact` | `32,32` | — （沿用左列） |
| `iconSize` / `.compact` | 20 / 16 | — |
| `leftStripeIcon.padding` | `5,5,5,5` | `5,5,5,2` |
| `rightStripeIcon.padding` | `5,5,5,5` | `5,2,5,5` |
| `leftStripeIcon.padding.compact` | `4,4,4,4` | `4,5,4,3` |
| `rightStripeIcon.padding.compact` | `4,4,4,4` | `4,3,4,5` |
| `leftStripeIconWithName.padding` | `4,4,4,4` | `4,6,4,2` |
| `rightStripeIconWithName.padding` | `4,4,4,4` | `4,2,4,6` |
| `leftStripeIconWithName.padding.compact` | `3,3,3,3` | `3,4,3,2` |
| `rightStripeIconWithName.padding.compact` | **不存在** | `3,2,3,4` |
| `left/rightStripeTextOffset` | 0 / 0 | **2 / −2** |

三件要点：

1. **按钮 37 宽 × 40 高，且左右镜像不对称**——靠窗口内侧留 2、外侧留 5，
   等于把图标推向窗口边缘。**只在常规档**：Islands 没覆盖 `size.compact`，
   紧凑下退回共同根的 32×32 正方。

2. **高亮矩形两套都是 30×30**。上游画背景是「组件矩形 − insets − iconPadding」
   （`SquareStripeButtonLook.kt:64-70 · paintBackground`）：expUI `40−5−5`、Islands `37−5−2`，
   都得 30。Islands 只是把这块整体推向条带外缘。
   > 复刻时这里原先画的是**整个按钮矩形**，于是 expUI 下高亮 40×40、贴着邻居也贴着条带边缘，
   > 比 Rider 胖一圈。改 Islands 时一并修正。

3. **缺 `.compact` ≠ 回落到代码兜底**。紧凑档的实现是把所有 `X.compact` 盖到 `X` 上
   （`LafManagerImpl.kt:1466-1474 · applyDensityOnUpdateUi`），没有 `.compact` 的键
   **不参与覆盖、保留基值**。所以 expUI 紧凑 + 显名时右条带是 `4` 而不是兜底的 `5`。
   （同一个函数下面的 `applyAltColors` 用同样的手法处理 §3 那个 `alt` 块的 `alt.` 前缀。）

---

## 5. 有意不做

| 上游 | 位置 | 为什么不做 |
|---|---|---|
| **渐变背景** | `IslandsGradientPainter.kt`（738 行）+ `gradientBackground-islands-dark.svg` | 那片彩色渐变的"海"是 JetBrains 的美术资产 + 一整套采样/缓存逻辑，与工具窗口布局正交。岛的形态不依赖它——纯色海已经是完整的 Islands。 |
| **失活变透明** | `Island.inactiveAlpha` = 0.56，`IslandsInactiveFrameGraphics2D.kt` | 依赖整帧重绘时替换 `Graphics2D`，Avalonia 没有对应位置；且它是窗口级观感，不影响布局。数字记在 §2，要做时有据。 |
| **`Islands Darcula`** | `ManyIslandsDarcula.theme.json` | 它是 Darcula 老配色 + 岛形态的组合档。先做 Dark / Light 两套，形态一致，配色多一套是纯加法。 |
| **`ide.ui.theme.custom.islands`** | `IslandsUICustomization.kt:338-366 · applyMissingKeys` | 「给自定义主题也套岛」的兼容路径，运行期用编辑器配色反推 `ToolWindow.background`。我们的主题是自己定义的，不需要反推。**注意这段只在 `IslandsState.isCustomEnabled()` 下跑**（`:345`）——读的时候别当成 Islands 主题本身的取值来源。 |

---

## 6. 落到 Forge 的形状

Islands 相对 expUI 多出**两个正交维度**：配色族（expUI / Islands）与形态（方角贴死 / 圆角留缝），
而原来 `ForgePalette` 只有颜色、`ForgeMetrics` 是一组全局 `const`、`ForVariant(variant)`
只按明暗二选一，三者都表达不了"这套主题下是 20，那套下没有"。落地成：

| 位置 | 做了什么 |
|---|---|
| `ForgeThemeFamily` | `ExpUi` / `Islands` 两个族，与明暗正交 |
| `ForgePalette.ForTheme(family, variant)` | 四套配色的入口；`ForVariant` 保留为 `ForTheme(ExpUi, …)` 的薄封装，既有宿主一行不用改 |
| `ForgePalette.Family` | 每套配色自报家门，控件据此取度量，不必让宿主再传一遍族 |
| `ForgePalette.MainBackground` / `IslandBorder` | 「海」与描边色。expUI 两套的 `MainBackground` 填各自的 `ToolWindow.background`，无岛时海=岛，退化成原样 |
| `ForgePalette.Island`（可空） | §2 的几何；`null` 即"不是岛"。**判族别拿颜色相等去猜** |
| `ForgeIslandMetrics` | arc / borderWidth / borderArcLength / insets，常规与紧凑两档 |
| `ForgeStripeButtonMetrics` + `ForgeInsets` | §4 的四份内边距 × 两档密度 × 两个族 |
| `ForgeIslandDecor.Apply` | 裁剪 + 描边，套在工具窗口面板与编辑区外层（对应上游的 `XNextIslandHolder` 与 `EditorsSplitters`） |
| `ForgeAppearance.Family` | 宿主的开关。设在外框上，编辑区继承（见[宿主接入指南 §5.0](../conventions/宿主接入指南.md)）。早先是两个控件各一个属性、要各设一遍，只设一个就得到「四边是岛、中间还是方角」 |

`ForgeMetrics` / `ForgeCompactMetrics` 里那几个条带按钮常量**保留但降级为「代码兜底」**——
它们是"主题一个键都没写时的值"，画界面一律走 `ForgeStripeButtonMetrics.ForTheme`。

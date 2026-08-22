# Piccolo.Forge

用 Avalonia / C#（.NET 8）**复刻 IntelliJ 平台**的工具窗口布局与主题。

**复刻 = 读懂上游的设计，然后自己写一遍**，不是源码翻译。

## 第一原则

**每一个行为、每一个数字，都要能指到上游源码的具体位置（文件 + 类/字段名）。
指不到的就是还没审明白，不许先写。**

上一次做 Rider 形态（在 `Piccolo.Bench` 里）是照着截图和肉眼推测建的——
当时误以为只有编译产物可查。结果把「面板停在哪」（`anchor`）和「按钮画在哪」
捏成了一个概念，模型错了一层。**那套不迁移，只作反面参照。**

## 上游参考

`third_party/intellij-community/`（稀疏浅克隆，不进本仓历史）。
拉的是这几块：

| 路径 | 里面有什么 |
|---|---|
| `platform/platform-impl/src/com/intellij/openapi/wm/impl/` | `WindowInfoImpl` `DesktopLayout` `ToolWindowManagerImpl` |
| `platform/platform-impl/src/com/intellij/toolWindow/` | `Stripe` `StripeButton` `InternalDecoratorImpl` `ResizeStripeManager` `defaultToolWindowlayoutProvider`，以及 **`expUI/` `xNext/`（新 UI，Rider 现在的样子）** |
| `platform/platform-api/src/com/intellij/openapi/wm/` | 对外接口 |
| `platform/platform-resources/src/themes/` | `darcula.theme.json` `Light.theme.json` **`expUI/`** |
| `platform/util/ui/src/com/intellij/util/ui/` | `JBUI` 等度量常量 |

重新拉：见 `docs/conventions/拉上游参考.md`。

## 审读产出

写代码之前先把上游审明白，产出在 `docs/upstream/`：

| 文档 | 内容 |
|---|---|
| [工具窗口模型](docs/upstream/工具窗口模型.md) | 字段、默认摆放、按钮落位、停靠几何、显示/隐藏规则 |
| [度量与主题键](docs/upstream/度量与主题键.md) | 条带/按钮/标题条的每个数字，以及主题键与 expUI 取值 |
| [编辑区](docs/upstream/编辑区.md) | 标签页、分屏树、关闭后选谁、上限淘汰、存档形状 |
| [Islands 主题](docs/upstream/Islands主题.md) | 圆角留缝那套：岛的画法、`Island.*` 键、两套配色的层号差异 |

接入这个库看：[宿主接入指南](docs/conventions/宿主接入指南.md) —— 哪些事归库、哪些事归你。

## 工程结构

| 工程 | 干什么 |
|---|---|
| `src/Piccolo.Forge.Model` | 纯模型，不依赖 Avalonia：`WindowInfo` / `DesktopLayout` / `ToolWindowManager` / 两个栈 / 默认布局 / 按钮落位 |
| `src/Piccolo.Forge.Mvvm` | 可观察包装：工具窗口与内容的 VM。**只依赖模型层，不引 Avalonia，也不引任何 MVVM 框架** |
| `src/Piccolo.Forge.Avalonia` | 控件：条带按钮 / 分隔符 / 条带 / 标题条 / 停靠区 / 整机框架；语言字典（`Locales/`） |
| `src/Piccolo.Forge.Themes.ExpUi` | 经典 expUI 主题：全部控件的 ControlTheme + 明暗颜色（`ForgeExpUiTheme`） |
| `src/Piccolo.Forge.Themes.Islands` | Islands 主题（`ForgeIslandsTheme`）。两个主题包互不依赖，引哪个就是哪套 |
| `tests/Piccolo.Forge.Tests` | xunit v3 + Avalonia headless（含 Skia 渲染快照） |
| `samples/Piccolo.Forge.Sample` | 示例：默认布局，条带按钮可点开/收起 |

渲染快照默认写到系统临时目录，`FORGE_SNAPSHOT_DIR` 可以改：

```bash
FORGE_SNAPSHOT_DIR=/tmp/forge dotnet test Piccolo.Forge.slnx
```

net8.0 / Avalonia 12.1.1，`TreatWarningsAsErrors` 与 `GenerateDocumentationFile` 全开
（测试与示例不强制 XML 注释）。包版本集中在 `Directory.Packages.props`。

## 现在能跑什么

- 新 UI（V2）默认布局：左右两根竖条带，上下两组，分组分隔符，BOTTOM 组倒序
- 点条带按钮开合工具窗口，「一格一个可见窗口」的互斥
- 同一条边两个窗口按 `sideWeight` 切成两半
- 拖分隔条改大小（分隔条 0px，热区 6px），拖条带按钮换位置
- 停靠几何：默认 TOP/BOTTOM 通栏，宽屏模式反过来
- 明暗两套 expUI 配色、紧凑模式度量、`JBUI.scale` 的取整规则
- **Islands 主题**（Rider 主题菜单里的 `Islands Dark` / `Islands Light`）：面板变成圆角、
  四周留缝、浮在「海」上。`Appearance.Family` 一处切换（外框设一次，编辑区继承），与明暗正交、各走各的。
  形态与配色照抄上游（`Island.arc` 20 / `borderWidth` 6 / `borderArcLength` 14），
  实现上用裁剪替掉 Swing 的「画完再盖」——差异与理由写在 `ForgeIslandDecor` 的注释里
- 布局存档读写（XML 形状与属性名照抄上游）
- 条带「显示名字」模式：宽度 59，名字按第一个空格拆成最多两行
- 浮动 / 独立窗口形态：两者标题与任务栏行为不同，bounds 三段式还原
- 内容区 tab：画在标题条同一行，选中带下划线，放不下的压成 0 宽；COMBO 只显示选中那块
- tab 上的关闭 / 固定：固定住的点一下是解固定不是关闭，Alt+点关掉其它所有
- 「更多」按钮：同一时刻只有一根条带显示，没东西可挑时整个隐藏
- 右键菜单、「更多」弹窗、浮动窗口淡出定时器
- Alt 双击并按住临时浮出条带的状态机（键盘钩子留给宿主接）
- **编辑区**：标签页（选中/固定/关闭/上限淘汰）、二叉分屏树、关闭后选谁的完整规则
- 编辑区背板：一张文档都没开时露出来的那一层（宿主给工厂，分屏后每个叶子各一份）
- 宿主接入：`ToolWindowShellViewModel` + `frame.Attach(shell)`，
  面板内容塞 `object`，视图由宿主自己的 ViewLocator / DataTemplate 解析；
  **开关面板不会重建任何已有控件**（有用例数构造次数钉住）

**上游已审但有意不做**（原因写在 `工具窗口模型.md` §17）：
真图标（那是 JetBrains 的美术资产，本项目按审出的尺寸自己画）、
工具窗口**内部**再嵌套分栏（编辑区的分屏已经做了）、
Alt 手势的键盘钩子（状态机已就位，钩子等宿主应用）。

```bash
dotnet test Piccolo.Forge.slnx
```

## 与 Piccolo.Bench 的关系

两个独立的仓，互不引用：Bench 复刻 VS Code 的 workbench，Forge 复刻 IntelliJ 平台。
配色体系不同（VS Code 的颜色 token vs IntelliJ 的主题键），两种形态本来互斥。

## 许可证

**暂未定，先闭源。** 开源与否、用哪个许可证，等做出来再议。

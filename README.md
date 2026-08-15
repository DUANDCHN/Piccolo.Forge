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

## 工程结构

| 工程 | 干什么 |
|---|---|
| `src/Piccolo.Forge.Model` | 纯模型，不依赖 Avalonia：`WindowInfo` / `DesktopLayout` / 默认布局 / 按钮落位 |
| `src/Piccolo.Forge.Themes` | 度量常量与主题键名 |
| `src/Piccolo.Forge.Avalonia` | 控件 |
| `tests/Piccolo.Forge.Tests` | xunit v3 + Avalonia headless |
| `samples/Piccolo.Forge.Sample` | 示例：把默认布局的按钮摆进左右两根条带 |

net8.0 / Avalonia 12.1.1，`TreatWarningsAsErrors` 与 `GenerateDocumentationFile` 全开
（测试与示例不强制 XML 注释）。包版本集中在 `Directory.Packages.props`。

```bash
dotnet test Piccolo.Forge.slnx
```

## 与 Piccolo.Bench 的关系

两个独立的仓，互不引用：Bench 复刻 VS Code 的 workbench，Forge 复刻 IntelliJ 平台。
配色体系不同（VS Code 的颜色 token vs IntelliJ 的主题键），两种形态本来互斥。

## 许可证

**暂未定，先闭源。** 开源与否、用哪个许可证，等做出来再议。

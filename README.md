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

## 与 Piccolo.Bench 的关系

两个独立的仓，互不引用：Bench 复刻 VS Code 的 workbench，Forge 复刻 IntelliJ 平台。
配色体系不同（VS Code 的颜色 token vs IntelliJ 的主题键），两种形态本来互斥。

## 许可证

**暂未定，先闭源。** 开源与否、用哪个许可证，等做出来再议。

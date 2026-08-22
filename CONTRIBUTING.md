# 开发说明

面向改这个库的人；使用者看 README.md。

## 原则

**每一个行为、每一个数字，都要能指到上游源码的具体位置（文件 + 类/字段名）。指不到的就是还没审明白，不许先写。**
复刻 = 读懂上游的设计再自己写一遍，不是源码翻译。

上游是 `third_party/intellij-community/`（稀疏浅克隆，不进本仓历史），拉法见 `docs/conventions/拉上游参考.md`。
审读产出在 `docs/upstream/`：工具窗口模型、度量与主题键、编辑区、Islands 主题。

## 工程结构

| 工程 | 干什么 |
|---|---|
| `src/Piccolo.Forge.Model` | 纯模型，不依赖 Avalonia |
| `src/Piccolo.Forge.Mvvm` | 可观察包装，只依赖模型层，不引 Avalonia 与任何 MVVM 框架（有测试钉住） |
| `src/Piccolo.Forge.Mvvm.CommunityToolkit` | 唯一允许引 MVVM 框架的包 |
| `src/Piccolo.Forge.Avalonia` | 控件、度量、调色板（`Themes/`）、语言字典（`Locales/`，13 种） |
| `src/Piccolo.Forge.Themes.ExpUi` / `.Islands` | 两套主题，互不依赖；缺控件或缺键 `ForgeThemeCompletenessTests` 点名 |
| `tests/Piccolo.Forge.Tests` | xunit v3 + Avalonia headless（含 Skia 渲染快照，`FORGE_SNAPSHOT_DIR` 指定输出目录） |
| `samples/Piccolo.Forge.Sample` | 试验场：`--islands` 换主题，顶部开关演示明暗 / 语言 / 显示名字 / 角标 |

```bash
dotnet test Piccolo.Forge.slnx
```

net8.0 + net10.0，警告即错误，公开成员必须有 XML 注释，包版本集中在 `Directory.Build.props` / `Directory.Packages.props`。

## 公开 API 账本

六个库各带 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`（PublicApiAnalyzers）。
新增公开成员要写进 Unshipped，否则 RS0016 报错；能 internal 的先 internal。发版规则见 `docs/conventions/发布.md`。

## 文档

- 宿主怎么接：`docs/conventions/宿主接入指南.md`
- 怎么发版：`docs/conventions/发布.md`
- 语言字典：加键必须 13 份一起加，`ForgeLocaleTests` 锁键集

## 与 Piccolo.Bench 的关系

两个独立的仓，互不引用：Bench 复刻 VS Code 的 workbench，Forge 复刻 IntelliJ 平台，两种形态互斥。

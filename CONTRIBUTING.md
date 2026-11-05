# 开发说明

面向改这个库的人；使用者看 [README.md](README.md)。项目自有代码采用 [MIT](LICENSE)，
提交贡献时请确认你有权按该许可证提供改动，并保留第三方材料的原始声明。

## 环境与验证

安装 .NET 10 SDK（最低 SDK 配置见 `global.json`）及 .NET 8 runtime，使用 Git 克隆本仓库。
普通构建、测试和运行示例不需要克隆上游仓库；生成色板才需要额外参考源码。

```bash
dotnet restore Piccolo.Forge.slnx
dotnet build Piccolo.Forge.slnx -c Release --no-restore
dotnet test Piccolo.Forge.slnx -c Release --no-build
dotnet pack Piccolo.Forge.slnx -c Release --no-build
```

使用工程默认输出目录。提交前关闭正在运行的示例及会锁定输出程序集的预览器。
当前 CI 仅在发布标签或手动触发时运行；提交 PR 前请在本地完成以上检查。

## 问题与拉取请求

- Bug 请提供最小复现、系统、.NET/Avalonia/Forge 版本、主题族、明暗和缩放比例；界面问题附截图。
- 修改应解释具体问题、行为变化及验证方式；涉及拖放、焦点、浮动窗口时加入对应回归验证。
- 行为和度量的上游依据写清路径、符号及已知提交；Forge 有意不同的行为写明理由。
- 不提交密钥、私人配置或构建产物，不自行更改包版本；版本由维护者在发布时统一调整。
- 提交信息使用 Conventional Commits 与中文描述；新增代码带必要注释，公开 API 带 XML 文档。

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
| `samples/` | 四个可运行示例与一个共享内容库，入口见 [示例说明](samples/README.md) |

```bash
dotnet test Piccolo.Forge.slnx
```

net8.0 + net10.0，警告即错误，公开成员必须有 XML 注释，包版本集中在 `Directory.Build.props` / `Directory.Packages.props`。

## 公开 API 账本

各可打包库带 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`（PublicApiAnalyzers）。
新增公开成员要写进 Unshipped，否则 RS0016 报错；能 internal 的先 internal。发版规则见 `docs/conventions/发布.md`。

## 文档

- 宿主怎么接：`docs/conventions/宿主接入指南.md`
- 怎么发版：`docs/conventions/发布.md`
- 语言字典：加键必须 13 份一起加，`ForgeLocaleTests` 锁键集

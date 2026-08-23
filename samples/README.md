# 示例

五个工程，**四个能跑、一个是内容库**：

| 工程 | 能跑 | 引哪些资源包 | 入口写法 | 看什么 |
|---|---|---|---|---|
| `Piccolo.Forge.Sample` | ✅ | `Themes.ExpUi` **一个** + 两个附加包 | `<forge:ForgeControlsTheme Family="ExpUi"/>` | **控件**：Avalonia 全部 89 个 |
| `Piccolo.Forge.Sample.ExpUi` | ✅ | `Themes.ExpUi` **一个** | `<forge:ForgeTheme Family="ExpUi"/>` | **外壳布局**：条带 / 停靠区 / 工具窗 / 编辑器标签 |
| `Piccolo.Forge.Sample.Islands` | ✅ | `Themes.Islands` **一个** | `<forge:ForgeTheme Family="Islands"/>` | 同上，Islands 档 |
| `Piccolo.Forge.Sample.Mvvm` | ✅ | `Themes.ExpUi` **一个** | `<forge:ForgeTheme Family="ExpUi"/>` | **MVVM 接法**：面板是 VM、视图由 ViewLocator 解析 |
| `Piccolo.Forge.Sample.Shared` | ❌ 类库 | **一个都不引** | — | 两个外壳示例的内容 |

**入口与包是两回事**：入口类只有三个（`ForgeTheme` 全套 / `ForgeControlsTheme` 只控件 /
`ForgeShellTheme` 只外壳，都在 `Piccolo.Forge.Avalonia` 里），族由 `Family` 属性定 ——
**包管"有哪些可用"，属性管"用哪个"**。族的资源在运行期按 avares 取：`Family` 选了没被引用的族，
启动即抛，异常里写明缺哪个包。两档都要的宿主（如 LDVision）两个资源包都引，
`Family="{x:Static local:Settings.Family}"` 从配置来，启动时求值一次，换族重启生效。

```bash
dotnet run --project samples/Piccolo.Forge.Sample            # 控件
dotnet run --project samples/Piccolo.Forge.Sample.ExpUi      # 外壳（经典档）
dotnet run --project samples/Piccolo.Forge.Sample.Islands    # 外壳（Islands 档）
dotnet run --project samples/Piccolo.Forge.Sample.Mvvm       # MVVM 接法
```

## `Sample.Mvvm` 演示什么

外壳示例的视图解析刻意从简（直接塞控件）；本示例演示**宿主该怎么组织代码**，
结构照 Dock 的 MvvmSample：`ViewModels/` 与 `Views/` 分目录、视图由 `ViewLocator` 按命名约定解析。

- 面板即 VM：继承 `ToolWindowPanel` / `EditorPanel`（CommunityToolkit 版），
  类里只有状态与命令，一行控件代码都没有
- 命令与通知由源生成器生成：`[ObservableProperty]` / `[RelayCommand]`（含 CanExecute 置灰）
- 跨面板通信走构造注入（任务面板拿着日志面板的回调，两者互不相识）
- 装配处唯一：`MainViewModel` 是全应用唯一知道"有哪些面板、各停哪条边"的地方
- 对接全靠绑定：`ForgeControl.Shell` 与 `ForgeEditorControl.Editor` 都是 StyledProperty，
  主窗口 XAML 两个 `{Binding}` 接完，**没有一行 code-behind 接线**
  （`Attach()` 那套命令式 API 仍在，给不走绑定的宿主用，两条路等价）
- 布局持久化：启动恢复（没有/坏档退默认摆放，不挡启动）、退出保存
  （`ShutdownRequested`）；编辑区内容按标签 id 由 `contentFactory` 重建。
  验证方法：跑起来把面板拖一拖、关掉重开 —— 日志面板第一行会写"已恢复上次布局"
- ⚠ `CommunityToolkit.Mvvm` 必须**直接引用**：源生成器不随传递引用流动，
  只靠传递引用能编译，特性却静默不生成代码
- ⚠ 示例的 `ViewLocator` 是反射版；AOT / 裁剪发布的宿主换编译期生成的
  （如 StaticViewLocator）或纯 DataTemplates，机制相同

## 为什么控件只演示一档、外壳要按档各出一个

**分档这条轴不是均匀的**，是量过的：

- **外壳**每族一份模板 —— 两族逐文件 diff，11 个文件**无一相同**（ContentTab 35 行、EditorTab 34 行…）。
  两档摆在一起看差别明显，所以按档各出一个示例。
- **控件**两档**共用同一份模板**（上游 Islands 的 parentTheme 就是 Experimental*，
  控件层结构相同、只有色板不同）。两档打开几乎一样，各出一个只是重复。

## 为什么按档拆成两个工程，而不是一个带 `--islands` 的

**设计的默认姿势是"谁用哪一档，就引哪一个资源包"**，所以四个工程里没有一个引两个主题包。
两档都引是少数派（宿主想在设置里切换，如 LDVision）——那种接法不在示例里：
两个资源包都引，`Family` 用 `{x:Static}` 从配置来，换档重启生效。入口类不用换。

单包示例不只是"演示对了"，它还是一道**构建期的证明**：`Themes.ExpUi` 若不足以单独装出
一整套界面（漏了键、或悄悄依赖了另一族的东西），那个示例会当场编不过、或跑出没颜色的控件。

## `Sample.Shared` 为什么是类库、为什么不可运行

它只装**外壳演示的内容**（停靠 / 工具窗 / 编辑器标签）。**不引任何主题包** —— 三个壳各引各的，
于是"一个宿主只引一个主题包"这件事在每个壳的 csproj 里一眼可见，而这份 89 条的清单只有一份。
抄成每档一份必然漂移，那正是 `ForgeThemeCompletenessTests` 立项要防的缺陷类。
控件目录不在这里 —— 它只有一个消费者（`Piccolo.Forge.Sample`），直接放在那个工程里，
于是它也是可运行的（Rider 里能跑、axaml 能用预览器）。

它不引主题包这条本身也是护栏：内容里一旦写死某族的键（`Forge.ExpUi.*` / `Forge.Islands.*`）
就编不过。控件的色键不带族前缀，所以两档能共用同一份内容。

> 试过改成"共享源码"（每个工程 link 同一批文件）好让每个工程都可运行，**走不通**：
> Avalonia 的 `x:Name` 源生成器不接受 link 进来的 axaml，补 `AdditionalFiles` 也不行，
> `Nav` / `Detail` 这些字段生不出来。

## 命令行开关（控件示例 `Piccolo.Forge.Sample`）

| 开关 | 作用 |
|---|---|
| `--dark` | 起暗色档 |
| `--open` | 展开下拉（弹出层里的模板只有弹开才走得到） |
| `--select <控件名>` | 直接定位到某个控件的页 |
| `--sweep` | 把 89 个条目逐个渲染一遍再退出，专扫"一渲染就崩"；exit code 可判 |

```bash
# 冒烟：明暗两档全过才算模板都装得起来
dotnet run --project samples/Piccolo.Forge.Sample -- --sweep --open
dotnet run --project samples/Piccolo.Forge.Sample -- --sweep --open --dark
```

⚠ `--sweep` 只扫崩溃，**扫不出"悄悄退回默认外观"** —— 资源找不到在 Avalonia 里不报错。
颜色和形状仍要人眼过一遍，挑控件多的页看（Calendar / DataGrid / MenuItem）。

## ⚠ 在 Rider 里构建整个解决方案会失败？

多半不是代码问题：**Rider 的 Avalonia 预览器会加载示例的 dll**，于是示例"正在运行"，
构建往 `bin` 里拷文件时被拒，报一堆 `MSB3021` / `MSB3027`（"文件被 .NET Host (xxxx) 锁定"）。

先关掉打开着的 axaml 标签页（或在 Settings → Languages & Frameworks → Avalonia 里关掉预览器），
再构建。判断"是不是真错误"的办法：看错误码——`MSB3021` / `MSB3027` 一律是拷贝失败，不是编译失败。

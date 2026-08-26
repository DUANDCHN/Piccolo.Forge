#!/usr/bin/env python3
"""从 IntelliJ 上游主题 JSON 生成 Forge 控件主题的色板 axaml。

    python3 scripts/gen-forge-palette.py

为什么用生成而不是手写：色值本来就是搬运的，手抄到第三个控件就出过一次事故
（按锚点插入时两档 Focus.Ring 值相同，同一块被插了两遍，运行期抛同名键异常直接崩）。
生成没有这个失败模式，而且"色值搬自上游"从一句注释变成可复核的映射表。

上游更新时重跑本脚本即可。改色请改 MAP 里的来源路径，不要去改生成物。
"""
import json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UP = os.path.join(ROOT, 'third_party', 'intellij-community'
                        '/platform/platform-resources/src/themes')
SRC = {
    ('ExpUi', 'Light'): f'{UP}/expUI/expUI_light.theme.json',
    ('ExpUi', 'Dark'): f'{UP}/expUI/expUI_dark.theme.json',
    ('Islands', 'Light'): f'{UP}/islands/ManyIslandsLight.theme.json',
    ('Islands', 'Dark'): f'{UP}/islands/ManyIslandsDark.theme.json',
}

# 上游主题的 parentTheme 链。很多键在 expUI / Islands 里没写，是**继承父主题**的，
# 只看单个文件会误判成"上游没定义"。id 在 vendored 的精简源码里查不到（plugin.xml 没带进来），
# 这张表按文件名与 id 的一一对应关系写死。
PARENTS = {
    'IntelliJ': f'{UP}/intellijlaf.theme.json',
    'Darcula': f'{UP}/darcula.theme.json',
    'ExperimentalLight': f'{UP}/expUI/expUI_light.theme.json',
    'ExperimentalDark': f'{UP}/expUI/expUI_dark.theme.json',
    'ExperimentalLightWithLightHeader': f'{UP}/expUI/expUI_light_with_light_header.theme.json',
}


class Lit:
    """字面兜底：上游 JSON 一路继承到顶都没有，值在 Java 代码里硬编码的那些。
    note 写清出处，让"这个值哪来的"可复核。"""

    def __init__(self, light, dark, note):
        self.light, self.dark, self.note = light, dark, note

    def value(self, variant):
        return self.light if variant == 'Light' else self.dark

# Forge 键 ← 上游 ui 路径（按顺序回落，第一个取到的为准）。
# 上游自己就是这么回落的：某档不写某个键时落到更通用的那条（如 *.background）。
MAP = [
    # 全局文字色。⚠ 少了这一档所有控件都是**黑字**：Avalonia 的 TextElement.Foreground
    # 默认值就是 Brushes.Black 且会继承，主题不设它时亮档看不出来、暗档全是黑字。
    ('Forge.Text.Primary',                ['*.foreground']),
    ('Forge.Text.Secondary',              ['*.infoForeground']),

    ('Forge.Button.Background',           ['Button.startBackground', '*.background']),
    ('Forge.Button.Border',               ['Button.startBorderColor']),
    ('Forge.Button.Accent.Background',    ['Button.default.startBackground']),
    ('Forge.Button.Accent.Foreground',    ['Button.default.foreground']),
    ('Forge.Button.Disabled.Foreground',  ['*.disabledText']),
    ('Forge.Button.Disabled.Border',      ['*.disabledBorderColor']),
    # 上游常规按钮没有 hover 态（DarculaButtonUI.getBackground 无 rollover 分支），
    # 这两档借 ActionButton 的叠加量，让全系统的悬停反馈是同一个强度。
    ('Forge.Overlay.Hover',               ['ActionButton.hoverBackground']),
    ('Forge.Overlay.Pressed',             ['ActionButton.pressedBackground']),
    ('Forge.Focus.Ring',                  ['*.focusColor']),

    ('Forge.Input.Background',            ['TextField.background', '*.background']),
    ('Forge.Input.Border',                ['Component.borderColor']),
    # 悬停边框 = **主题色**，与 Forge.Focus.Ring 同源（*.focusColor）。
    # 悬停让 1px 的边框本身变成主题色，聚焦另有一圈 2px 的外环（PART_FocusRing）——
    # 两者都是主题色但形状不同，所以「悬停」和「已聚焦」仍分得开。
    # ⚠ 它归 ForgeAccent 的实心填充档：宿主换种子色时必须跟着换，否则整屏都换了、
    #   只有输入框悬停还是库里那支蓝。
    # ⚠ 只换颜色不换粗细：Border 改 BorderThickness 会挤压内容区，鼠标移上去框里的
    #   文字会挪 1px，看着就是抖。要「变粗」只能像焦点环那样在外面再叠一层。
    ('Forge.Input.HoverBorder',           ['*.focusColor']),
    # 聚焦时**边框自己**变主题色。
    # ⚠ 这是**刻意偏离上游**：expUI 画聚焦态是「框外一圈 2px 的 focusColor 光晕 + 内边框转成
    #   与底同色（Component.focusedBorderColor = 白 / #2B2D30）」，两层合起来才读成「这个框在编辑」。
    #   本库把那圈光晕去掉了（宿主反馈：看着像边框加粗，而框里的文字位置又没动，别扭），
    #   于是颜色必须由内边框自己扛 —— 再取 Component.focusedBorderColor 就是一圈看不见的白边。
    # ⚠ 它归 ForgeAccent 的实心填充档：宿主换种子色时必须跟着换。
    ('Forge.Input.FocusedBorder',         ['*.focusColor']),
    ('Forge.Input.Disabled.Background',   ['*.disabledBackground']),
    ('Forge.Input.Disabled.Border',       ['*.disabledBorderColor']),
    ('Forge.Input.Placeholder',           ['*.infoForeground']),
    ('Forge.Input.SelectionBackground',   ['*.selectionBackground']),
    ('Forge.Input.SelectionForeground',   ['*.selectionForeground']),
    ('Forge.Input.Error',                 ['Component.errorFocusColor']),
    ('Forge.Input.Warning',               ['Component.warningFocusColor']),

    # 不可编辑的 ComboBox（我们的主要用法）上游有意做得比输入框亮一档 —— 更像按钮。
    ('Forge.ComboBox.Background',         ['ComboBox.nonEditableBackground',
                                           'ComboBox.ArrowButton.nonEditableBackground',
                                           'ComboBox.background', '*.background']),
    ('Forge.ComboBox.EditableBackground', ['ComboBox.background', 'TextField.background', '*.background']),
    ('Forge.ComboBox.Disabled.Background',['ComboBox.disabledBackground', '*.disabledBackground']),

    ('Forge.Popup.Background',            ['Popup.background', '*.background']),
    ('Forge.Popup.Border',                ['Popup.borderColor']),

    ('Forge.Item.HoverBackground',        ['List.hoverBackground', '*.hoverBackground']),
    ('Forge.Item.SelectedBackground',     ['*.selectionBackground']),
    ('Forge.Item.SelectedForeground',     ['*.selectionForeground']),
    ('Forge.Item.SelectedInactiveBackground', ['*.selectionInactiveBackground']),

    # 滚动条。取 Transparent 那组（浮在内容上，悬停时轨道才微微显现），不是 Opaque 组。
    # expUI 两档都没写 ScrollBar，Islands.Dark 写了 —— 差异靠 parentTheme 链与字面兜底解决。
    # 字面值来自 ScrollBarPainter.java 的 key(light, dark, "键名")，那里的 int 已经是 ARGB，不需换序。
    ('Forge.ScrollBar.Thumb', ['ScrollBar.Transparent.thumbColor', 'ScrollBar.thumbColor',
                               Lit('#33737373', '#47A6A6A6', 'ScrollBarPainter.THUMB_BACKGROUND')]),
    ('Forge.ScrollBar.ThumbBorder', ['ScrollBar.Transparent.thumbBorderColor', 'ScrollBar.thumbBorderColor',
                                     Lit('#33595959', '#47383838', 'ScrollBarPainter.THUMB_FOREGROUND')]),
    ('Forge.ScrollBar.ThumbHover', ['ScrollBar.Transparent.hoverThumbColor', 'ScrollBar.hoverThumbColor',
                                    Lit('#47737373', '#59A6A6A6', 'ScrollBarPainter.THUMB_HOVERED_BACKGROUND')]),
    ('Forge.ScrollBar.ThumbHoverBorder', ['ScrollBar.Transparent.hoverThumbBorderColor',
                                          'ScrollBar.hoverThumbBorderColor',
                                          Lit('#47595959', '#59383838', 'ScrollBarPainter.THUMB_HOVERED_FOREGROUND')]),
    ('Forge.ScrollBar.Track', ['ScrollBar.Transparent.trackColor', 'ScrollBar.trackColor',
                               Lit('#00808080', '#00808080', 'ScrollBarPainter.TRACK_BACKGROUND')]),
    ('Forge.ScrollBar.TrackHover', ['ScrollBar.Transparent.hoverTrackColor', 'ScrollBar.hoverTrackColor',
                                    Lit('#1A808080', '#1A808080', 'ScrollBarPainter.TRACK_HOVERED_BACKGROUND')]),

    # 菜单。上游的菜单面板与 Popup 同底；分隔线与快捷键文本各有专门的色。
    ('Forge.Menu.Background', ['PopupMenu.background', 'Menu.background', 'Popup.background', '*.background']),
    ('Forge.Menu.Border', ['Menu.borderColor', 'Popup.borderColor']),
    ('Forge.Menu.Separator', ['Menu.separatorColor', '*.separatorColor']),
    # 菜单选中：**实心主题色 + 白字**。
    # ⚠ 刻意偏离上游的 *.selectionBackground（淡蓝底 + 黑字）：那一档在列表/树里合适
    #   （一屏几十行，淡底才不闹），菜单是一次只选一行的短列表，淡底读起来"像没选中"
    #   （2026-08-26 宿主实机："感觉很淡"）。Rider 自己的动作菜单也是实心蓝 + 白字。
    # ⚠ 底色归 ForgeAccent 的实心填充档，跟着宿主的种子色走；前景取与主按钮同一支白。
    ('Forge.MenuItem.SelectedBackground', ['*.focusColor']),
    ('Forge.MenuItem.SelectedForeground', ['Button.default.foreground']),
    # 快捷键提示（Ctrl+S 那截）比正文弱一档，选中时上游也不提亮 —— 有 acceleratorSelectionForeground
    # 的档位它与常态同值，没有的档位就回落到常态，两种情况结果一致。
    ('Forge.MenuItem.Accelerator', ['*.acceleratorForeground', '*.shortcutForeground']),
    ('Forge.MenuItem.AcceleratorSelected', ['*.acceleratorSelectionForeground', '*.acceleratorForeground']),
    ('Forge.MenuItem.Disabled', ['*.disabledForeground', '*.disabledText']),

    # 复选框 / 单选钮共用一组色（上游的 SVG 也共用 Checkbox.* 这套 id）。
    # Light 档在 icons.ColorPalette；Dark 档那里是空的 —— 上游把暗色档整个重定向到
    # /themes/expUI/icons/dark/*.svg，颜色写死在那些 SVG 文件里，故用字面兜底。
    ('Forge.Check.Background', ['icons:Checkbox.Background.Default',
                                Lit('#FFFFFFFF', '#FF2B2D30', 'expUI/icons/dark/checkBox.svg')]),
    ('Forge.Check.Border', ['icons:Checkbox.Border.Default',
                            Lit('#FFA8ADBD', '#FF5A5D63', 'expUI/icons/dark/checkBox.svg')]),
    ('Forge.Check.SelectedBackground', ['icons:Checkbox.Background.Selected',
                                        Lit('#FF3574F0', '#FF3574F0', 'expUI/icons/dark/checkBoxSelected.svg')]),
    ('Forge.Check.SelectedBorder', ['icons:Checkbox.Border.Selected',
                                    Lit('#FF3574F0', '#FF3574F0', 'expUI/icons/dark/checkBoxSelected.svg')]),
    ('Forge.Check.Glyph', ['icons:Checkbox.Foreground.Selected',
                           Lit('#FFFFFFFF', '#FFFFFFFF', 'expUI/icons/dark/checkBoxSelected.svg')]),
    ('Forge.Check.Disabled.Background', ['icons:Checkbox.Background.Disabled',
                                         Lit('#FFF7F8FA', '#FF393B40', 'expUI/icons/dark/checkBoxDisabled.svg')]),
    ('Forge.Check.Disabled.Border', ['icons:Checkbox.Border.Disabled',
                                     Lit('#FFDFE1E5', '#FF5A5D63', 'expUI/icons/dark/checkBoxDisabled.svg')]),
    ('Forge.Check.Disabled.Glyph', ['icons:Checkbox.Foreground.Disabled',
                                    Lit('#FFC9CCD6', '#FF6F737A', 'expUI/icons/dark/checkBoxSelectedDisabled.svg')]),

    # 分组分隔线（Expander / GroupBox）。与菜单分隔线分开：暗色档上游给菜单单独定了
    # Menu.separatorColor(Gray4)，比通用的 *.separatorColor(Gray3) 亮一档。
    ('Forge.Group.Separator', ['Group.separatorColor', '*.separatorColor']),

    # 选项卡。上游的 TabbedPane 是对话框/设置页里那种标签（不是编辑器标签 —— 那套在 Forge 外壳里）。
    # 选中态是**浅底色块**而非下划线：expUI 给的是 focusColor，老 UI 的 DefaultTabs.underlineColor
    # 那套下划线做法在新 UI 里已经不用了。
    ('Forge.Tab.SelectedBackground', ['TabbedPane.focusColor']),
    ('Forge.Tab.HoverBackground', ['TabbedPane.hoverColor']),
    ('Forge.Tab.ContentBorder', ['TabbedPane.contentAreaColor']),

    ('Forge.Slider.Track', ['Slider.trackColor']),
    ('Forge.Slider.Thumb', ['Slider.buttonColor', 'Button.startBackground', '*.background']),
    ('Forge.Slider.ThumbBorder', ['Slider.buttonBorderColor']),
    ('Forge.Slider.Tick', ['Slider.tickColor']),

    ('Forge.Progress.Bar', ['ProgressBar.progressColor']),
    ('Forge.Progress.Track', ['ProgressBar.trackColor']),
    ('Forge.Progress.Passed', ['ProgressBar.passedColor']),
    ('Forge.Progress.Failed', ['ProgressBar.failedColor']),

    # ⚠ 提示气泡在**亮色档也是深底浅字** —— 上游 ToolTip.background 取的是 Gray2/Gray3
    # 这类深色。别"顺手"改成浅底，那就不是 IntelliJ 的提示了。
    ('Forge.ToolTip.Background', ['ToolTip.background']),
    ('Forge.ToolTip.Foreground', ['ToolTip.foreground']),
    ('Forge.ToolTip.Border', ['ToolTip.borderColor']),

    # 链接。新 UI **不分访问态** —— 上游 active/hover/pressed/visited 四个值相同，
    # 所以这里只取一个，不做"已访问变紫"那套。
    ('Forge.Link.Foreground', ['Link.activeForeground']),

    ('Forge.Label.Error', ['Label.errorForeground']),
    ('Forge.Label.Warning', ['Label.warningForeground']),

    # 拆分按钮在**蓝底态**（上游叫 default button，也就是我们的选中态）下那条分隔线。
    # 常态那条用 Forge.Button.Border 就行，蓝底上它看不见 —— 上游为此单独给了一个键。
    ('Forge.Split.AccentSeparator', ['Button.Split.default.separatorColor']),

    # 表格。行的悬停/选中不另立键，复用 Forge.Item.*（与列表、树同源，上游也是同一套）。
    ('Forge.Table.GridLine', ['Table.gridColor']),
    ('Forge.Table.Stripe', ['Table.stripeColor']),
    ('Forge.Table.HeaderSeparator', ['TableHeader.separatorColor', 'TableHeader.bottomSeparatorColor']),

    # 通知卡。与提示气泡同理：**亮色档也是深底浅字**，这是上游的做法。
    # ★ 这三个**有意偏离上游**（2026-08-23，用户拍板）：
    #   上游 expUI 的右下角通知在**亮色档也是深色**（Notification.background = Gray2 #27282E，
    #   metadata 明确写着 "Notification that appears in bottom-right corner"），
    #   与"亮色主题"的直觉相悖。这里改成跟随主题：
    #     亮色 → 白底（同弹出层），深色字由控件继承宿主正文色
    #     暗色 → 仍用上游的 Notification.background（Gray3），它比弹出层的 Gray2 亮一档，
    #            正好让通知在暗色界面上"浮"起来
    #   前景**不设**，继承宿主正文色 —— 亮底得黑字、暗底得浅字，正好两档都对。
    ('Forge.Notification.Background', [Lit('#FFFFFFFF', '#FF393B40',
                                           '有意偏离上游：亮色档改浅底，暗色档仍用 Notification.background')]),
    ('Forge.Notification.Border', [Lit('#FFB9BDC9', '#FF43454A',
                                       '亮色档用弹出层的边框色，暗色档用上游 Notification.borderColor')]),
    ('Forge.Notification.Link', ['Notification.linkForeground']),
    # 关闭 / 更多按钮的悬停底（上游为通知上的图标按钮专门给的）。
    ('Forge.Notification.IconHover', ['Notification.iconHoverBackground']),
    # ⚠ 阴影极淡：上游 Notification.Shadow 的四边都是 #80808010 / #00000010（约 6% 不透明）。
    ('Forge.Notification.Shadow', ['Notification.Shadow.bottom1Color']),

    # 窗口装饰（自绘标题条 / 边框 / 那几颗按钮）。上游是 TitlePane 那一族。
    ('Forge.Window.Background',           ['*.background']),
    ('Forge.Window.Border',               ['Borders.color']),
    # ⚠ 暗档上游没写 TitlePane.background，回落到 *.background —— 上游自己就是这么继承的。
    ('Forge.Window.TitleBarBackground',   ['TitlePane.background', '*.background']),
    # ⚠ 标题条按钮的悬停**不取上游**：TitlePane.Button.hoverBackground 两档都是白色叠加
    #   （亮档 #FFFFFF1A），在浅色标题条上等于看不见。这一档统一走 Forge.Overlay.Hover。

    # 抽屉浮出时压在内容上的遮罩。⚠ 上游**没有**这个色：IntelliJ 的模态是系统窗口，
    # 不在 IDE 画面上蒙一层，全树搜 modalityOverlay 一无所获。这里自定：亮档 25% 黑、暗档 45% 黑。
    ('Forge.Scrim', [Lit('#40000000', '#73000000', '上游无模态遮罩色，自定')]),

    # 工具窗内的**横幅**通知 —— 与上面的气泡通知是两回事：浅底深字、按严重度换整卡底色。
    ('Forge.Banner.InfoBackground', ['Notification.ToolWindow.informativeBackground']),
    ('Forge.Banner.InfoBorder', ['Notification.ToolWindow.informativeBorderColor']),
    ('Forge.Banner.WarnBackground', ['Notification.ToolWindow.warningBackground']),
    ('Forge.Banner.WarnBorder', ['Notification.ToolWindow.warningBorderColor']),
    ('Forge.Banner.ErrorBackground', ['Notification.ToolWindow.errorBackground']),
    ('Forge.Banner.ErrorBorder', ['Notification.ToolWindow.errorBorderColor']),
    ('Forge.Banner.Foreground', ['Notification.ToolWindow.informativeForeground']),

    # 校验提示（DataValidationErrors）。上游给了成套的背景 / 边框 / 前景。
    ('Forge.Validation.ErrorBackground', ['ValidationTooltip.errorBackground']),
    ('Forge.Validation.ErrorBorder', ['ValidationTooltip.errorBorderColor']),
    ('Forge.Validation.ErrorForeground', ['ValidationTooltip.errorForeground']),
    ('Forge.Validation.WarningBackground', ['ValidationTooltip.warningBackground']),
    ('Forge.Validation.WarningBorder', ['ValidationTooltip.warningBorderColor']),
    ('Forge.Validation.WarningForeground', ['ValidationTooltip.warningForeground']),
]

# 度量。上游的 arc 是**直径**（Java RoundRectangle2D 后两参），Avalonia 的 CornerRadius 是半径，故 ÷2。
METRICS = '''    <!-- ═══ 圆角：全系统**一个值 4**（圆形/胶囊除外）═══
         上游其实给了整套刻度（arc 是直径，÷2 才是 Avalonia 的 CornerRadius）：
             Component.arc.compact=6 → 3   紧凑控件
             Button.arc / Component.arc / MainToolbar.Button.arc.compact = 8 → 4
             Banner.arc=12（compact 8）→ 6 / 4   提示条
             Island.arc.compact=16（Island.arc=20）→ 8 / 10   面板
         本软件**有意不分档**，一律 4：分档只有在层级足够深（IDE 的 Island 套 Banner 套控件）
         时才读得出来；本软件的面板外形已由 Forge 外壳定，里面再分三档只会显得零碎。
         2026-08-23 决定，此前上位机 101 处写死值散在 1~21 之间。

         两个例外**不是设计选择、是几何**，别顺手改成 4：
           · Forge.Focus.RingCornerRadius = 4 + 环厚 2 = 6，改了焦点环与边框不同心；
           · Forge.Radio.CornerRadius = 尺寸 16 ÷ 2 = 8，那是"画成圆"，不是圆角。 -->
    <CornerRadius x:Key="Forge.CornerRadius">4</CornerRadius>

    <!-- 按钮前景两档都不给，继承宿主正文色（上游 Light 档也是继承）。
         主按钮的 Accent.Foreground 是例外：蓝底上必须白字。 -->

    <CornerRadius x:Key="Forge.Button.CornerRadius">4</CornerRadius>
    <x:Double x:Key="Forge.Button.MinWidth">72</x:Double>
    <x:Double x:Key="Forge.Button.MinHeight">28</x:Double>
    <Thickness x:Key="Forge.Button.Padding">14,4</Thickness>
    <Thickness x:Key="Forge.Button.BorderThickness">1</Thickness>

    <!-- 聚焦环：上游画在边框**外围**（DarculaUIUtil.doPaint），宽 2；
         外圈圆角＝内圆角＋环宽，才是同心的。 -->
    <Thickness x:Key="Forge.Focus.RingThickness">2</Thickness>
    <CornerRadius x:Key="Forge.Focus.RingCornerRadius">6</CornerRadius>

    <!-- 输入类度量：上游 TextField.minimumSize 49,28、Component.arc 8（→半径 4）。 -->
    <CornerRadius x:Key="Forge.Input.CornerRadius">4</CornerRadius>
    <x:Double x:Key="Forge.Input.MinWidth">49</x:Double>
    <x:Double x:Key="Forge.Input.MinHeight">28</x:Double>
    <Thickness x:Key="Forge.Input.Padding">8,3</Thickness>
    <Thickness x:Key="Forge.Input.BorderThickness">1</Thickness>
    <!-- 输入框左右两个内嵌槽（前缀标签、框内小按钮）与正文之间的间距。
         取 8 = Forge.Input.Padding 的水平档，跟同一条水平节奏；不另调一个数。
         ⚠ 没有它时标签与正文**贴在一起**，读起来像正文的一部分（宿主实机 2026-08-26）。 -->
    <Thickness x:Key="Forge.Input.AffixLeftMargin">0,0,8,0</Thickness>
    <Thickness x:Key="Forge.Input.AffixRightMargin">8,0,0,0</Thickness>
    <!-- NumericUpDown 走上游 Spinner.minimumSize 72,28；箭头区宽度用 Component.arrowAreaWidth。 -->
    <x:Double x:Key="Forge.Spinner.MinWidth">72</x:Double>
    <x:Double x:Key="Forge.Spinner.ArrowAreaWidth">28</x:Double>

    <!-- ComboBox：上游 padding 是 Java Insets(top,left,bottom,right)=1,9,1,6，
         换成 Avalonia 的 Thickness(left,top,right,bottom)。箭头区同 Component.arrowAreaWidth。 -->
    <Thickness x:Key="Forge.ComboBox.Padding">9,1,6,1</Thickness>
    <x:Double x:Key="Forge.ComboBox.ArrowAreaWidth">28</x:Double>
    <!-- 滚动条：上游没把厚度放进 theme.json（在 Java 侧按 DPI 算），12 是与 IntelliJ 观感相当的值。
         thumb 内缩 2px 留出轨道边距 —— 上游 mac 分支用 macMargin 做同一件事，非 mac 分支靠布局留白。 -->
    <x:Double x:Key="Forge.ScrollBar.Thickness">12</x:Double>
    <Thickness x:Key="Forge.ScrollBar.ThumbMargin">2</Thickness>

    <!-- 进度条：上游没给厚度（Java 侧按 DPI 算），4 是与 IntelliJ 观感相当的值。 -->
    <x:Double x:Key="Forge.Progress.Thickness">4</x:Double>
    <CornerRadius x:Key="Forge.Progress.CornerRadius">4</CornerRadius>

    <!-- 提示气泡。 -->
    <Thickness x:Key="Forge.ToolTip.Padding">8,4</Thickness>
    <CornerRadius x:Key="Forge.ToolTip.CornerRadius">4</CornerRadius>

    <!-- 开关（ToggleSwitch）。上游没有这个控件（Swing 用复选框代替），
         尺寸按 IntelliJ 新 UI 里设置页那种开关的观感定：轨道 28×16、滑块 12。 -->
    <x:Double x:Key="Forge.Switch.TrackWidth">28</x:Double>
    <x:Double x:Key="Forge.Switch.TrackHeight">16</x:Double>
    <x:Double x:Key="Forge.Switch.KnobSize">12</x:Double>

    <!-- 选项卡：高度与内边距来自上游 TabbedPane.tabHeight / tabInsets(Java Insets 换序)。
         ⚠ 圆角直接取 tabSelectionArc=4，**不按 Button.arc 那样折半** ——
           那个键名是 "SelectionArc"，指的就是选中块的圆角半径，与进 RoundRectangle2D
           当直径用的 Button.arc 不是同一个口径。 -->
    <x:Double x:Key="Forge.Tab.Height">40</x:Double>
    <Thickness x:Key="Forge.Tab.Padding">12,0</Thickness>
    <CornerRadius x:Key="Forge.Tab.CornerRadius">4</CornerRadius>

    <!-- 表格：表头高度取上游 TableHeader.height 的 os.default（25），
         行高与列表项同档（上游 List.rowHeight=24）。 -->
    <x:Double x:Key="Forge.Table.HeaderHeight">25</x:Double>
    <x:Double x:Key="Forge.Table.RowHeight">24</x:Double>
    <Thickness x:Key="Forge.Table.CellPadding">8,0</Thickness>

    <!-- 滑块。上游只给了颜色没给尺寸（Java 侧按 DPI 算），这两个数是与 IntelliJ 观感相当的值。 -->
    <x:Double x:Key="Forge.Slider.ThumbSize">14</x:Double>
    <x:Double x:Key="Forge.Slider.TrackThickness">4</x:Double>

    <!-- 树的每层缩进 = 上游 Tree.leftChildIndent(7) + rightChildIndent(11)。
         ⚠ 上游 Tree.paintLines = false —— **不画层级连接线**，层级只靠缩进表达。
         行高走 Forge.Item.Height（expUI 把 Tree/List 的 rowHeight 都定成 24）。 -->
    <!-- ⚠ 这是**一层的量**（x:Double），由 ForgeTreeIndentConverter 乘 TreeViewItem.Level
         折成行内 Margin。曾经是 Thickness（Forge.Tree.ChildMargin）直接挂在 ItemsPresenter 上，
         那样会把行的高亮底连同子树一起推开，选中条逐级变短 —— 上游 DefaultTreeUI 是按整行宽度
         画选中背景的，缩进只作用于内容。 -->
    <x:Double x:Key="Forge.Tree.Indent">18</x:Double>
    <Thickness x:Key="Forge.Tree.Padding">12,4</Thickness>
    <Thickness x:Key="Forge.List.Padding">0,4</Thickness>

    <!-- Expander：标题行比菜单项高一档（它是分组标题，不是列表项）。
         展开标记用 chevron 而非三角 —— 上游 Tree.collapsedIcon / expandedIcon 就是
         chevronRight.svg / chevronDown.svg，收起朝右、展开朝下。 -->
    <x:Double x:Key="Forge.Expander.HeaderHeight">28</x:Double>
    <Thickness x:Key="Forge.Expander.HeaderPadding">6,0</Thickness>
    <Thickness x:Key="Forge.Expander.ContentPadding">22,4,0,8</Thickness>

    <!-- 复选框 / 单选钮。上游的 SVG 是 24 画布里 `rect x=4.5 y=4.5 w=15 h=15 rx=2.5`
         （0.5 偏移是让 1px 描边落在像素中心的老技巧，实际占位含描边是 16），
         故这里取 16×16（圆角随全局的 4；上游 arc 折半是 3，本软件不分档）。
         单选钮同上游 `circle r=8`，也是 16 直径、圆角即半径 —— 那是"画成圆"，不参与统一。
         iconTextGap 4 来自 ui.CheckBox.textIconGap。 -->
    <x:Double x:Key="Forge.Check.Size">16</x:Double>
    <CornerRadius x:Key="Forge.Check.CornerRadius">4</CornerRadius>
    <CornerRadius x:Key="Forge.Radio.CornerRadius">8</CornerRadius>
    <Thickness x:Key="Forge.Check.TextGap">4,0,0,0</Thickness>

    <!-- 菜单：行高与下拉项同档（上游未在 theme.json 给菜单项高度）。
         图标/勾选列 20 宽 —— 有图标和没图标的项文本要对齐，这一列恒占位。 -->
    <x:Double x:Key="Forge.MenuItem.Height">24</x:Double>
    <x:Double x:Key="Forge.MenuItem.IconColumnWidth">20</x:Double>
    <Thickness x:Key="Forge.MenuItem.Padding">6,0,10,0</Thickness>
    <Thickness x:Key="Forge.Menu.Padding">0,4</Thickness>
    <!-- 菜单栏（顶栏那条）的项更紧凑，且不吃菜单面板的行高。 -->
    <Thickness x:Key="Forge.MenuBarItem.Padding">10,4</Thickness>

    <!-- 下拉项行高走上游 List.rowHeight=24。 -->
    <x:Double x:Key="Forge.Item.Height">24</x:Double>
    <Thickness x:Key="Forge.Item.Padding">8,0</Thickness>
    <CornerRadius x:Key="Forge.Popup.CornerRadius">4</CornerRadius>
    <Thickness x:Key="Forge.Popup.Padding">0,4</Thickness>
'''

HEAD = {
    'ExpUi': '''      经典档（IntelliJ expUI）色板。**本文件由 scripts/gen-forge-palette.py 生成，不要手改。**
      色值逐条来自上游 expUI_{light,dark}.theme.json（Forge 仓 third_party/intellij-community），
      改色请改生成器里的 MAP，然后重跑。''',
    'Islands': '''      Islands 档色板。**本文件由 scripts/gen-forge-palette.py 生成，不要手改。**
      来源：上游 themes/islands/ManyIslands{Light,Dark}.theme.json。
      Islands 是 expUI 的一层皮肤（parentTheme=Experimental*），控件结构与经典档完全相同，
      **只有色板不同** —— 所以 Controls/ 下的模板两档共用，别为 Islands 另写模板。''',
}


def to_argb(v):
    """上游 JSON 是 #RRGGBBAA，Avalonia 是 #AARRGGBB。抄反了不报错，只是颜色悄悄不见。"""
    if not isinstance(v, str):
        return None
    h = v.lstrip('#')
    if not re.fullmatch(r'[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8}', h):
        return None
    return '#FF' + h.upper() if len(h) == 6 else '#' + h[6:].upper() + h[:6].upper()


def load(path):
    with open(path, encoding='utf-8') as fh:
        return json.loads(re.sub(r'//.*', '', fh.read()))


def resolve_in(theme, dotted):
    """只在这一份主题里找。找到返回 ARGB，没有返回 None。

    `icons:<键名>` 走 icons.ColorPalette 段 —— 复选框/单选钮的颜色在那儿，
    因为上游它们是 SVG 图标，颜色靠调色板替换而不是 ui 属性。
    """
    if dotted.startswith('icons:'):
        pal = theme.get('icons', {}).get('ColorPalette', {})
        node = pal.get(dotted[len('icons:'):])
        if node is None:
            return None
        colors, seen = theme.get('colors', {}), 0
        while isinstance(node, str) and node in colors and seen < 10:
            node, seen = colors[node], seen + 1
        return to_argb(node)

    # ⚠ 上游的键名**本身可能含点**（如 "Notification.Shadow": {...}、"MenuBar.borderColor"），
    #   不能简单按 . 分段下钻。这里贪心匹配：每一步都先试最长的键名。
    node, parts, i = theme['ui'], dotted.split('.'), 0
    while i < len(parts):
        if not isinstance(node, dict):
            return None
        for j in range(len(parts), i, -1):
            key = '.'.join(parts[i:j])
            if key in node:
                node, i = node[key], j
                break
        else:
            return None

    # 上游有些键按操作系统分叉，如 Menu.borderColor = {os.default, os.windows}。
    # 取 windows 那支：本软件是 Windows 上位机，mac 只是开发机。
    if isinstance(node, dict):
        node = node.get('os.windows', node.get('os.default'))
    pal, seen = theme.get('colors', {}), 0
    while isinstance(node, str) and node in pal and seen < 10:
        node, seen = pal[node], seen + 1
    return to_argb(node)


def resolve(theme, sources, variant):
    """按上游的优先级解析一个键。返回 (值, 出处说明)。

    ⚠ 顺序是**每一层内先走完 sources，再上升到父主题** —— 不是每个 source 各自走完整条链。
      反过来写会让父主题的具体键盖过本主题的通配键，后果是实打实的错色：
      expUI 的 `*.background` 是它有意重定义的中性灰 #2B2D30，而 darcula 里有具体的
      `Popup.background = #3C3F41`（expUI 存在的意义正是换掉那套老灰）。
      若让父层的具体键先赢，弹出层就会比周围亮一档、偏色。2026-08-22 加链式回落时踩过。

    Lit（字面兜底）永远最后用：整条链都没有才轮到它。
    """
    cur, hops = theme, 0
    while cur is not None and hops < 8:
        for src in sources:
            if isinstance(src, Lit):
                continue
            got = resolve_in(cur, src)
            if got:
                where = '' if (hops == 0 and src is sources[0]) else \
                        f'  <!-- {"回落自 " + src if hops == 0 else f"继承自父主题的 {src}"} -->'
                return got, where
        parent = cur.get('parentTheme')
        cur = load(PARENTS[parent]) if parent in PARENTS else None
        hops += 1

    for src in sources:
        if isinstance(src, Lit):
            return src.value(variant), f'  <!-- 上游 JSON 未定义，取自 {src.note} -->'
    return None, None


def main():
    missing = [p for p in SRC.values() if not os.path.exists(p)]
    if missing:
        sys.exit('找不到上游主题 JSON（需要同级的 Piccolo.Forge 仓）：\n  ' + '\n  '.join(missing))

    for family in ('ExpUi', 'Islands'):
        blocks = []
        for variant in ('Light', 'Dark'):
            theme = load(SRC[(family, variant)])
            lines = []
            for key, sources in MAP:
                val, note = resolve(theme, sources, variant)
                if not val:
                    sys.exit(f'{family}.{variant}: 键 {key} 在上游取不到值（来源 {sources}）')
                lines.append(f'            <SolidColorBrush x:Key="{key}" Color="{val}" />{note}')
            blocks.append(f'        <ResourceDictionary x:Key="{variant}">\n'
                          + '\n'.join(lines) + '\n        </ResourceDictionary>')

        out = (f'<ResourceDictionary xmlns="https://github.com/avaloniaui" '
               f'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\n'
               f'    <!--\n{HEAD[family]}\n    -->\n'
               f'    <ResourceDictionary.ThemeDictionaries>\n\n'
               + '\n\n'.join(blocks)
               + f'\n\n    </ResourceDictionary.ThemeDictionaries>\n\n{METRICS}</ResourceDictionary>\n')
        path = os.path.join(ROOT, 'src', f'Piccolo.Forge.Themes.{family}', 'Controls.Colors.axaml')
        with open(path, 'w', encoding='utf-8') as fh:
            fh.write(out)
        print(f'✓ {os.path.relpath(path, ROOT)}  ({len(MAP)} 键 × 2 档)')


if __name__ == '__main__':
    main()

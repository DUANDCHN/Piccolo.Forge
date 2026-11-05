# Third-party notices

The root MIT license covers project-owned code, not the third-party material below.
Retain these notices when redistributing relevant material. Product names identify
references, not affiliation or endorsement.

## Source map / 来源范围

| Material | Location in Forge | Source and terms |
|---|---|---|
| Generated palette values and documented metrics | `scripts/gen-forge-palette.py`, both `Themes.*/Controls.Colors.axaml` files, palette definitions under `src/Piccolo.Forge.Avalonia/Themes/`, `docs/upstream/` | IntelliJ Platform theme JSON and referenced source; preserve Apache-2.0 attribution and [license text](licenses/Apache-2.0.txt). Mappings and generated files include Forge-specific changes. |
| Editor move transactions and drag lifecycle | Editor code identified by Dock references | Dock MIT; pinned reference below |
| Locale/add-on organization and template notes | Locale/theme integration and documentation | Semi.Avalonia MIT; references below |
| Built-in vector geometry | `src/Piccolo.Forge.Avalonia/Themes/ForgeIcons.cs` | Described in source as Forge-authored geometry; applications supply their own domain icons |
| NuGet dependencies | `Directory.Packages.props` and project references | Separately licensed dependencies; consult each package's license |

The original IntelliJ reference commit was not retained in this repository. File/symbol
references are not a complete per-file legal clearance; see [the review](docs/open-source-review.md).
No external font, bitmap, SVG or icon-font files are tracked at the review baseline.

## Dock

Forge 的 `DocumentDockFactory` 搬移事务边界（先从 source detach、再向 target attach、
最后一次性提交），以及标签拖拽的“行内仅预览、离开标签条后捕获、松手时提交”生命周期，
改编自：

> 借的是**行为**，不是词汇：Forge 用自己的 `EditorTab` / `EditorGroup` / `EditorDropSide`
> 建模，不复刻 Dock 的 `IDockable` / `IDocumentDock` / `DockOperation` 等类型。


- Project: Dock
- Repository: https://github.com/wieslawsoltes/Dock
- Commit: `3f2473fae54cf3c9b5079bd56c3612334a88f6ca`
- License: MIT
- Copyright (c) Wiesław Šoltés

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Semi.Avalonia

以下内容参考 Semi.Avalonia 实现（借的是**做法**，不含其源代码）：

- 语言文案机制：文案进资源字典、`ApplyLocale` 按 culture 换字典，与 `SemiTheme.Locale` 同一机制；
- 附加包拆分惯例：DataGrid / ColorPicker 的主题独立成包、只依赖对应的 Avalonia 控件包、不依赖主题主包；
- 控件模板化过程中的若干框架事实（必需的 PART 名、ControlTheme 对子类的匹配行为等）。

视觉结构与颜色不参考 Semi —— 那些以 IntelliJ Platform 源码为依据（见下一节）。

- Project: Semi.Avalonia
- Repository: https://github.com/irihitech/Semi.Avalonia
- License: MIT
- Copyright (c) IRIHI Technology

## IntelliJ Platform

Forge 的工具窗口模型（anchor + isSplit、条带 / 停靠区 / 标题条的布局规则）、度量常量与 expUI /
Islands 两族的配色，按 IntelliJ Platform 的源码与行为重做（见 `docs/upstream/`）。
C# 代码为独立实现，未复制 Java/Kotlin 源文件；度量、键名与颜色值取自上游。

- Project: IntelliJ Platform (intellij-community)
- Repository: https://github.com/JetBrains/intellij-community
- License: Apache License 2.0
- Copyright 2000-2026 JetBrains s.r.o.

Licensed under the Apache License, Version 2.0 (the "License"); you may not use the referenced
material except in compliance with the License. You may obtain a copy of the License at
http://www.apache.org/licenses/LICENSE-2.0. Unless required by applicable law or agreed to in
writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

The complete Apache-2.0 text is distributed in `licenses/Apache-2.0.txt`.
The current upstream [NOTICE.txt](https://github.com/JetBrains/intellij-community/blob/master/NOTICE.txt)
contains the following attribution, retained here for reference:

> This software includes code from IntelliJ IDEA
> Copyright (C) JetBrains s.r.o.
> https://www.jetbrains.com/idea/

Check notices and terms at the actual source revision used; this current reference does
not establish which revision generated the existing palettes. Semi.Avalonia's MIT terms
are the MIT text reproduced above, with IRIHI Technology as copyright holder.

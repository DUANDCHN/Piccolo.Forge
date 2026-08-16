# Third-party notices

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

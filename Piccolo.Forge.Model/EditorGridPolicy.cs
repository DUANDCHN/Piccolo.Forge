namespace Piccolo.Forge.Model;

/// <summary>
/// 「把 N 页排成 R×C 宫格」到底会排成什么形状 —— 唯一真值。
/// </summary>
/// <remarks>
/// 单独拎出来，是因为**两个调用方必须看同一份答案**：
/// <list type="number">
/// <item><see cref="EditorArea.ArrangeGrid"/> 按它建树；</item>
/// <item>宿主的命令栏按它决定某个预设**点了到底会不会变样**（不会变样就该禁用，
/// 否则用户点下去纹丝不动，看着像坏了）。</item>
/// </list>
/// 两边各算一次的话，迟早会算出两个不同的形状。
/// <para>
/// <b>格数按页数封顶</b>（<c>R*C</c> 与页数取小）：生来为空的格子没有内容可显示，
/// 编辑区的不变量（<see cref="EditorArea"/> 里的 <c>EnforceInvariants</c>）也不允许空组存在，
/// 所以预设的语义是「**最大**形状」而不是「精确形状」。
/// 多出来的页塞进最后一格当标签页。
/// </para>
/// </remarks>
public static class EditorGridPolicy
{
    /// <summary>
    /// 算出实际形状。
    /// </summary>
    /// <param name="tabCount">要排的页数。</param>
    /// <param name="rows">预设行数，<c>&lt;= 0</c> 表示按页数自动。</param>
    /// <param name="cols">预设列数，<c>&lt;= 0</c> 表示按页数自动。</param>
    /// <returns>实际行数、实际列数、实际格数。页数为 0 时全为 0。</returns>
    public static (int Rows, int Cols, int CellCount) Resolve(int tabCount, int rows, int cols)
    {
        if (tabCount <= 0)
        {
            return (0, 0, 0);
        }

        // 自动：cols = ⌈√n⌉、rows = ⌈n/cols⌉（2→1×2，4→2×2，9→3×3）
        if (rows <= 0 || cols <= 0)
        {
            cols = (int)Math.Ceiling(Math.Sqrt(tabCount));
            rows = (int)Math.Ceiling(tabCount / (double)cols);
        }

        var cellCount = Math.Clamp(rows * cols, 1, tabCount);
        var actualCols = Math.Min(cols, cellCount);
        var actualRows = (int)Math.Ceiling(cellCount / (double)actualCols);
        return (actualRows, actualCols, cellCount);
    }

    /// <summary>
    /// 这个预设在当前页数下**排得出与众不同的形状**吗？
    /// </summary>
    /// <remarks>
    /// 页数不够时预设会互相塌陷成同一个形状（2 页时 1×2 / 2×2 / 3×3 都只能排成一行两格）。
    /// 命令栏据此禁用塌陷掉的那些按钮 —— 「点了没反应」与「本来就点不了」，
    /// 后者才是诚实的界面。
    /// </remarks>
    /// <param name="tabCount">要排的页数。</param>
    /// <param name="rows">这个预设的行数。</param>
    /// <param name="cols">这个预设的列数。</param>
    /// <param name="smaller">排在它前面的那些预设（更小的形状）。</param>
    /// <returns>形状与 <paramref name="smaller"/> 里任何一个都不同则为 <see langword="true"/>。</returns>
    public static bool IsDistinct(int tabCount, int rows, int cols, params (int Rows, int Cols)[] smaller)
    {
        if (tabCount < 2)
        {
            return false;
        }

        var shape = Resolve(tabCount, rows, cols);
        foreach (var (r, c) in smaller)
        {
            var other = Resolve(tabCount, r, c);
            if (other.Rows == shape.Rows && other.Cols == shape.Cols)
            {
                return false;
            }
        }

        return true;
    }
}

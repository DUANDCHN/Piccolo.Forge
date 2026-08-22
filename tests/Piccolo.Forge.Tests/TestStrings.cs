using System.Runtime.CompilerServices;
using Piccolo.Forge.Avalonia;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 测试里的菜单文案断言都是中文写的：库默认英文（<see cref="ForgeStrings.English"/>），
/// 这里在程序集加载时用 Override 把文案钉成简体中文（测试里不装资源字典），免得每条用例各设一遍。
/// </summary>
internal static class TestStrings
{
    [ModuleInitializer]
    internal static void UseChinese() => ForgeStrings.Override = ForgeStrings.ChineseSimplified;
}

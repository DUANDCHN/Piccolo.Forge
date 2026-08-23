using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Piccolo.Forge.Sample.Mvvm;

/// <summary>
/// 视图定位器：VM 类型名去掉 "ViewModel" 换成 "View"，按名字找视图类（Dock 的 MvvmSample 同款）。
/// </summary>
/// <remarks>
/// 外壳把面板 VM 交给 ContentPresenter 时，Avalonia 会拿着 VM 来问它 ——
/// <see cref="Match"/> 认所有 <see cref="ObservableObject"/>（本示例的面板基类最终都继承它），
/// <see cref="Build"/> 造出对应的 View 并由框架设好 DataContext。
/// <para>
/// ⚠ 这是**反射**版（<c>Type.GetType</c>），示例够用；AOT / 裁剪发布的宿主要换成
/// 编译期生成的定位器（如 StaticViewLocator）或纯 DataTemplates —— 机制相同，只是解析提前到编译期。
/// </para>
/// </remarks>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control Build(object? data)
    {
        var name = data!.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is null)
            return new TextBlock { Text = $"找不到视图：{name}" };

        return (Control)Activator.CreateInstance(type)!;
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is ObservableObject;
}

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Piccolo.Forge.Mvvm;

/// <summary>
/// 最小的可观察基类。
/// </summary>
/// <remarks>
/// 本层**不引任何 MVVM 框架** —— 宿主用 CommunityToolkit 还是 ReactiveUI 是宿主的事，
/// 控件库不该替它选。所以这里自己写一个够用的。
/// <para>
/// <b>为什么不叫 <c>ObservableObject</c></b>：那个名字在 CommunityToolkit.Mvvm 和
/// ReactiveUI 里都有。宿主一旦 <c>using Piccolo.Forge.Mvvm;</c> 就会和它自己的
/// MVVM 框架撞成 CS0104（LDVision 接入时当场撞了）。
/// 库不该往宿主的命名空间里塞一个注定重名的类型。
/// </para>
/// </remarks>
public abstract class ForgeObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 值变了就写回并发通知。
    /// </summary>
    /// <typeparam name="T">属性类型。</typeparam>
    /// <param name="field">后备字段。</param>
    /// <param name="value">新值。</param>
    /// <param name="propertyName">属性名，编译器填。</param>
    /// <returns>确实变了则为 <see langword="true"/>。</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>发一个属性变更通知。</summary>
    /// <param name="propertyName">属性名，编译器填。</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

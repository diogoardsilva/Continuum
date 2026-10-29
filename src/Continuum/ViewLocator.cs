using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Continuum;

/// <summary>Shows FooViewModel with Views.FooView.</summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        var name = data!.GetType().FullName!.Replace(".ViewModels.", ".Views.").Replace("ViewModel", "View");
        var type = Type.GetType(name);
        return type is null
            ? new TextBlock { Text = "Vista em falta: " + name }
            : (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ObservableObject && data.GetType().Name.EndsWith("ViewModel");
}

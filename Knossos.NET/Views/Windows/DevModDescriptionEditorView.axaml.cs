using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Knossos.NET.ViewModels;
using System.ComponentModel;

namespace Knossos.NET.Views;

public partial class DevModDescriptionEditorView : Window
{
    public DevModDescriptionEditorView()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    public void BindTextBox()
    {
        var textbox = this.FindControl<TextBox>("PlainText");
        if (DataContext != null)
        {
            var vm = DataContext as DevModDescriptionEditorViewModel;
            if (vm != null)
            {
                vm.inputControl = textbox;
            }
        }
    }

    public void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if(DataContext != null)
        {
            var vm = DataContext as DevModDescriptionEditorViewModel;
            if(vm != null)
            {
                vm.Closing();
            }
        }
    }
}
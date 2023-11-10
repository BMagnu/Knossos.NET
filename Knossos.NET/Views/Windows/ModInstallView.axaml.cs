using Avalonia.Controls;
using Knossos.NET.ViewModels;
using System;
using System.ComponentModel;

namespace Knossos.NET.Views
{
    public partial class ModInstallView : Window
    {
        public ModInstallView()
        {
            InitializeComponent();
            this.Closing += ForceCollectTrash;
        }

        private void ForceCollectTrash(object? sender, CancelEventArgs e)
        {
            GC.Collect();
        }

        private void CheckBox_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = this.DataContext as ModInstallViewModel;
            if(vm != null)
            {
                vm.UpdateSpace();
            }
        }
    }
}

using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.NightSage;

[Export(typeof(ResourceDictionary))]
public partial class Options : ResourceDictionary {
    public Options() {
        InitializeComponent();
    }

    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e) {
        if (sender is PasswordBox box && box.DataContext is NightSagePlugin plugin) {
            plugin.ApiKey = box.Password;
        }
    }
}

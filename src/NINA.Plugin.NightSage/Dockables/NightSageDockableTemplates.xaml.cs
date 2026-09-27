using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.NightSage.Dockables;

[Export(typeof(ResourceDictionary))]
public partial class NightSageDockableTemplates : ResourceDictionary {
    public NightSageDockableTemplates() {
        InitializeComponent();
    }
}

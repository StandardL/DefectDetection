using DefectDetection.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace DefectDetection.Views;

public sealed partial class OfflinePage : Page
{
    public OfflineViewModel ViewModel
    {
        get;
    }

    public OfflinePage()
    {
        ViewModel = App.GetService<OfflineViewModel>();
        InitializeComponent();
    }
}

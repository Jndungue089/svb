using BeneditaUI.ViewModels;

namespace BeneditaUI.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<SettingsViewModel>();
    }
}

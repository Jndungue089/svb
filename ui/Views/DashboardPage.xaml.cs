using BeneditaUI.ViewModels;

namespace BeneditaUI.Views;

public partial class DashboardPage : ContentPage
{
    private DashboardViewModel? _vm;

    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = _vm = ServiceHelper.GetService<DashboardViewModel>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm?.StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm?.StopAutoRefresh();
    }
}

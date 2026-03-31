using BeneditaUI.ViewModels;

namespace BeneditaUI.Views;

public partial class EntitiesPage : ContentPage
{
    public EntitiesPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<EntitiesViewModel>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EntitiesViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}

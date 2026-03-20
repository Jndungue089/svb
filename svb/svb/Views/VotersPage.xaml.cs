using BeneditaUI.ViewModels;

namespace BeneditaUI.Views;

public partial class VotersPage : ContentPage
{
    public VotersPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<VotersViewModel>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is VotersViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}

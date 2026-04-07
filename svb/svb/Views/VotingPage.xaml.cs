using BeneditaUI.ViewModels;

namespace BeneditaUI.Views;

public partial class VotingPage : ContentPage
{
    public VotingPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<VotingViewModel>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is VotingViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}

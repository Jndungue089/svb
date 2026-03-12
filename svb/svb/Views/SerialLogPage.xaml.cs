using BeneditaUI.ViewModels;

namespace BeneditaUI.Views;

public partial class SerialLogPage : ContentPage
{
    public SerialLogPage()
    {
        InitializeComponent();
        var vm = ServiceHelper.GetService<SerialLogViewModel>();
        BindingContext = vm;

        // Auto-scroll when a new log entry arrives
        vm.Log.CollectionChanged += (_, _) =>
        {
            if (vm.Log.Count > 0)
                MainThread.BeginInvokeOnMainThread(() =>
                    LogList.ScrollTo(vm.Log[^1], ScrollToPosition.End, animate: false));
        };
    }
}

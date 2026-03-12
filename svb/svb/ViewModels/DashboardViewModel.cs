using BeneditaUI.Models;
using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeneditaUI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiService _api;

    public DashboardViewModel(ApiService api) => _api = api;

    [ObservableProperty]
    private ObservableCollection<VoteResult> _results = new();

    [ObservableProperty]
    private int _totalVoters;

    [ObservableProperty]
    private int _votersVoted;

    [ObservableProperty]
    private int _votersPending;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _apiOnline;

    [ObservableProperty]
    private string _statusMessage = "Aguardando...";

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Atualizando...";

        // Ping
        ApiOnline = await _api.PingAsync();

        // Results
        var results = await _api.GetResultsAsync();
        Results.Clear();
        if (results is not null)
            foreach (var r in results)
                Results.Add(r);

        // Voters summary
        var voters = await _api.GetVotersAsync();
        if (voters is not null)
        {
            TotalVoters   = voters.Count;
            VotersVoted   = voters.Count(v => !v.CanVote);
            VotersPending = voters.Count(v => v.CanVote);
        }

        StatusMessage = ApiOnline
            ? $"Última atualização: {DateTime.Now:HH:mm:ss}"
            : "⚠ API offline — verifique a conexão";

        IsLoading = false;
    }

    // Auto-refresh a cada 10 s quando a página estiver ativa
    private CancellationTokenSource? _autoRefreshCts;

    public void StartAutoRefresh()
    {
        _autoRefreshCts = new CancellationTokenSource();
        _ = AutoRefreshLoop(_autoRefreshCts.Token);
    }

    public void StopAutoRefresh() => _autoRefreshCts?.Cancel();

    private async Task AutoRefreshLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RefreshAsync();
            try { await Task.Delay(10_000, token); }
            catch (TaskCanceledException) { break; }
        }
    }
}

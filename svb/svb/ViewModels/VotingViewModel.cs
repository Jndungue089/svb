using BeneditaUI.Models;
using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeneditaUI.ViewModels;

public partial class VotingViewModel : ObservableObject
{
    private readonly ApiService _api;

    public VotingViewModel(ApiService api) => _api = api;

    // ── Entidades disponíveis ─────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<Entity> _entities = new();

    [ObservableProperty]
    private Entity? _selectedEntity;

    // ── Estado da votação ─────────────────────────────────────
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isVoting;

    /// <summary>Resultado visível após a votação (sucesso ou erro).</summary>
    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _voteSuccess;

    [ObservableProperty]
    private string _resultTitle = string.Empty;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private string _resultVoterName = string.Empty;

    // ── Computed properties ───────────────────────────────────

    /// <summary>True quando uma entidade está seleccionada e não está a votar nem a mostrar resultado.</summary>
    public bool CanVote =>
        SelectedEntity is not null && !IsVoting && !HasResult;

    /// <summary>True quando nenhuma entidade está seleccionada.</summary>
    public bool IsEntitySelected => SelectedEntity is not null;

    partial void OnSelectedEntityChanged(Entity? value)
    {
        OnPropertyChanged(nameof(CanVote));
        OnPropertyChanged(nameof(IsEntitySelected));
        // Reset resultado ao mudar de entidade
        if (HasResult) ResetVote();
    }

    partial void OnIsVotingChanged(bool value)  => OnPropertyChanged(nameof(CanVote));
    partial void OnHasResultChanged(bool value) => OnPropertyChanged(nameof(CanVote));

    // ── Comandos ──────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        var entities = await _api.GetEntitiesAsync();
        Entities.Clear();
        if (entities is not null)
            foreach (var e in entities)
                Entities.Add(e);
        IsLoading   = false;
        SelectedEntity = null;
        ResetVote();
    }

    /// <summary>
    /// Inicia a sessão de votação para a entidade seleccionada.
    /// Bloqueia a UI enquanto aguarda que o eleitor coloque o dedo.
    /// </summary>
    [RelayCommand]
    public async Task StartVoteAsync()
    {
        if (SelectedEntity is null) return;

        IsVoting   = true;
        HasResult  = false;

        var (ok, msg, voterName) = await _api.InitiateVoteAsync(SelectedEntity.Id);

        IsVoting = false;
        HasResult = true;
        VoteSuccess = ok;

        if (ok)
        {
            ResultTitle     = "Voto Registado!";
            ResultVoterName = voterName;
            ResultMessage   = $"O voto de «{voterName}» na entidade «{SelectedEntity.Acronym}» foi registado com sucesso.";
        }
        else
        {
            ResultTitle     = "Votação Falhada";
            ResultVoterName = string.Empty;
            ResultMessage   = TranslateError(msg);
        }
    }

    private static string TranslateError(string msg) => msg switch
    {
        var m when m.Contains("Voto ja realizado", StringComparison.OrdinalIgnoreCase)
                || m.Contains("Voto ja registado", StringComparison.OrdinalIgnoreCase)
                || m.Contains("ja votou",           StringComparison.OrdinalIgnoreCase)
            => "Este eleitor já exerceu o seu voto. Cada eleitor só pode votar uma vez.",
        var m when m.Contains("TIMEOUT",            StringComparison.OrdinalIgnoreCase)
            => "Tempo esgotado — o eleitor não colocou o dedo a tempo.",
        var m when m.Contains("PORTA_SERIAL_FECHADA", StringComparison.OrdinalIgnoreCase)
            => "Sensor não ligado — verifique a ligação USB.",
        var m when m.Contains("NAO_AUTORIZADO",     StringComparison.OrdinalIgnoreCase)
                || m.Contains("nao cadastrado",      StringComparison.OrdinalIgnoreCase)
            => "Impressão digital não reconhecida ou eleitor não cadastrado.",
        var m when m.Contains("ERRO_REGISTO",       StringComparison.OrdinalIgnoreCase)
            => "Falha ao registar o voto na base de dados.",
        _ => msg.Replace("_", " ")
    };

    /// <summary>Limpa o resultado e permite votar novamente.</summary>
    [RelayCommand]
    public void ResetVote()
    {
        HasResult       = false;
        VoteSuccess     = false;
        ResultTitle     = string.Empty;
        ResultMessage   = string.Empty;
        ResultVoterName = string.Empty;
    }
}

using BeneditaUI.Models;
using BeneditaUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeneditaUI.ViewModels;

public partial class EntitiesViewModel : ObservableObject
{
    private readonly ApiService _api;

    public EntitiesViewModel(ApiService api) => _api = api;

    [ObservableProperty]
    private ObservableCollection<Entity> _entities = new();

    [ObservableProperty]
    private Entity? _selectedEntity;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _feedbackMessage = string.Empty;

    [ObservableProperty]
    private Color _feedbackColor = Colors.Gray;

    [ObservableProperty]
    private bool _hasFeedback;

    // ── Formulário ────────────────────────────────────────────
    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newAcronym = string.Empty;

    [ObservableProperty]
    private string _newDescription = string.Empty;

    // ── Aviso duplicado em tempo real ─────────────────────────
    [ObservableProperty]
    private string _duplicateWarning = string.Empty;

    [ObservableProperty]
    private bool _hasDuplicateWarning;

    partial void OnNewAcronymChanged(string value) => CheckDuplicates();
    partial void OnNewNameChanged(string value)    => CheckDuplicates();

    private void CheckDuplicates()
    {
        if (!string.IsNullOrWhiteSpace(NewAcronym) &&
            Entities.Any(e => e.Acronym.ToUpper() == NewAcronym.Trim().ToUpper()))
        {
            DuplicateWarning    = $"Já existe uma entidade com a sigla «{NewAcronym.Trim().ToUpper()}».";
            HasDuplicateWarning = true;
            return;
        }
        if (!string.IsNullOrWhiteSpace(NewName) &&
            Entities.Any(e => e.Name.ToLower() == NewName.Trim().ToLower()))
        {
            DuplicateWarning    = $"Já existe uma entidade com o nome «{NewName.Trim()}».";
            HasDuplicateWarning = true;
            return;
        }
        DuplicateWarning    = string.Empty;
        HasDuplicateWarning = false;
    }

    private void SetFeedback(string msg, bool isError)
    {
        FeedbackMessage = msg;
        FeedbackColor   = isError
            ? Color.FromArgb("#B53B2A")
            : Color.FromArgb("#2E7D52");
        HasFeedback = !string.IsNullOrEmpty(msg);
    }

    // ── Load ──────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        var entities = await _api.GetEntitiesAsync();
        Entities.Clear();
        if (entities is not null)
            foreach (var e in entities)
                Entities.Add(e);
        IsLoading = false;
        SetFeedback($"{Entities.Count} entidade(s) carregada(s).", false);
        CheckDuplicates();
    }

    // ── Add ───────────────────────────────────────────────────

    [RelayCommand]
    public async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAcronym))
        {
            SetFeedback("A sigla da entidade é obrigatória.", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(NewName))
        {
            SetFeedback("O nome completo da entidade é obrigatório.", true);
            return;
        }

        // Verificação local anti-duplicado
        if (Entities.Any(e => e.Acronym.ToUpper() == NewAcronym.Trim().ToUpper()))
        {
            SetFeedback($"Já existe uma entidade com a sigla «{NewAcronym.Trim().ToUpper()}».", true);
            return;
        }
        if (Entities.Any(e => e.Name.ToLower() == NewName.Trim().ToLower()))
        {
            SetFeedback($"Já existe uma entidade com o nome «{NewName.Trim()}».", true);
            return;
        }

        IsLoading = true;
        var (ok, msg) = await _api.AddEntityAsync(
            NewName.Trim(),
            NewAcronym.Trim().ToUpper(),
            string.IsNullOrWhiteSpace(NewDescription) ? null : NewDescription.Trim());

        SetFeedback(ok ? $"Entidade «{NewAcronym.Trim().ToUpper()}» cadastrada com sucesso." : msg, !ok);

        if (ok)
        {
            NewName        = string.Empty;
            NewAcronym     = string.Empty;
            NewDescription = string.Empty;
            await LoadAsync();
        }
        IsLoading = false;
    }

    // ── Delete ────────────────────────────────────────────────

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedEntity is null)
        {
            SetFeedback("Selecione uma entidade na lista primeiro.", true);
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            "Remover Entidade",
            $"Tem a certeza que deseja remover «{SelectedEntity.Name}» ({SelectedEntity.Acronym})?\n\nEsta ação não pode ser desfeita.",
            "Sim, remover", "Cancelar");

        if (!confirm) return;

        IsLoading = true;
        var (ok, msg) = await _api.DeleteEntityAsync(SelectedEntity.Id);
        SetFeedback(ok ? $"Entidade «{SelectedEntity.Acronym}» removida." : msg, !ok);

        if (ok) await LoadAsync();
        IsLoading = false;
    }
}

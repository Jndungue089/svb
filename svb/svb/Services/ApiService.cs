using BeneditaUI.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeneditaUI.Services;

public class ApiService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService(HttpClient http) => _http = http;

    // ── VOTERS ────────────────────────────────────────────────

    public async Task<List<Voter>?> GetVotersAsync()
    {
        try { return await _http.GetFromJsonAsync<List<Voter>>("voters", _json); }
        catch { return null; }
    }

    public async Task<(bool Ok, string Message, Voter? Voter)> RegisterVoterAsync(string name, string bi)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("voters", new { name, bi });
            if (res.IsSuccessStatusCode)
            {
                var voter = await res.Content.ReadFromJsonAsync<Voter>(_json);
                return (true, "Eleitor cadastrado!", voter);
            }
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"Erro {(int)res.StatusCode}: {body}", null);
        }
        catch (Exception ex) { return (false, ex.Message, null); }
    }

    public async Task<(bool Ok, string Message, Voter? Voter)> EnrollFingerAsync(int voterId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var res = await _http.PostAsync($"voters/{voterId}/enroll", null, cts.Token);
            if (res.IsSuccessStatusCode)
            {
                var voter = await res.Content.ReadFromJsonAsync<Voter>(_json);
                return (true, "Impressão digital registada!", voter);
            }
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"Erro {(int)res.StatusCode}: {body}", null);
        }
        catch (OperationCanceledException) { return (false, "Timeout — verifique o sensor.", null); }
        catch (Exception ex)              { return (false, ex.Message, null); }
    }

    public async Task<(bool Ok, string Message)> DeleteVoterAsync(int id)
    {
        try
        {
            var res = await _http.DeleteAsync($"voters/{id}");
            return res.IsSuccessStatusCode
                ? (true, "Eleitor removido.")
                : (false, $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── ENTITIES ──────────────────────────────────────────────

    public async Task<List<Entity>?> GetEntitiesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<Entity>>("entities", _json); }
        catch { return null; }
    }

    public async Task<(bool Ok, string Message)> AddEntityAsync(string name, string acronym, string? description)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("entities", new { name, acronym, description });
            if (res.IsSuccessStatusCode) return (true, "Entidade cadastrada!");
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"Erro {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Message)> DeleteEntityAsync(int id)
    {
        try
        {
            var res = await _http.DeleteAsync($"entities/{id}");
            return res.IsSuccessStatusCode
                ? (true, "Entidade removida.")
                : (false, $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── VOTING (iniciado pelo painel) ─────────────────────────

    /// <summary>
    /// Envia a entidade escolhida para a API e aguarda que o eleitor
    /// coloque o dedo no sensor (até 38 s).
    /// </summary>
    public async Task<(bool Ok, string Message, string VoterName)> InitiateVoteAsync(int entityId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(38));
            var res = await _http.PostAsJsonAsync("vote/initiate", new { entityId }, cts.Token);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<VoteInitiateResponse>(_json);
                return (true, "Voto registado com sucesso!", body?.NomeEleitor ?? "");
            }
            var errBody = await res.Content.ReadFromJsonAsync<VoteInitiateResponse>(_json);
            return (false, errBody?.Mensagem ?? $"Erro {(int)res.StatusCode}", "");
        }
        catch (OperationCanceledException) { return (false, "Tempo esgotado — coloque o dedo mais depressa.", ""); }
        catch (Exception ex)              { return (false, ex.Message, ""); }
    }

    public async Task<(bool Ok, string Message, int FingerId, string VoterName)> IdentifyVoterAsync(string? bi = null)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(38));
            var res = await _http.PostAsJsonAsync("vote/identify", new { bi }, cts.Token);
            var body = await res.Content.ReadFromJsonAsync<VoteScanResponse>(_json);

            if (res.IsSuccessStatusCode && body is not null)
                return (true, body.Mensagem ?? "Biometria validada.", body.FingerId, body.NomeEleitor ?? string.Empty);

            return (false, body?.Mensagem ?? $"Erro {(int)res.StatusCode}", 0, string.Empty);
        }
        catch (OperationCanceledException) { return (false, "Tempo esgotado na leitura biométrica.", 0, string.Empty); }
        catch (Exception ex) { return (false, ex.Message, 0, string.Empty); }
    }

    public async Task<(bool Ok, string Message)> ConfirmVoteAsync(int fingerId, int entityId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("vote/confirm", new { fingerId, entityId });
            var body = await res.Content.ReadFromJsonAsync<VoteActionResponse>(_json);
            return res.IsSuccessStatusCode
                ? (true, body?.Mensagem ?? "Voto confirmado.")
                : (false, body?.Mensagem ?? $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Message)> CancelVoteAsync()
    {
        try
        {
            var res = await _http.PostAsync("vote/cancel", null);
            var body = await res.Content.ReadFromJsonAsync<VoteActionResponse>(_json);
            return res.IsSuccessStatusCode
                ? (true, body?.Mensagem ?? "Voto cancelado.")
                : (false, body?.Mensagem ?? $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── VOTE RESULTS ──────────────────────────────────────────

    public async Task<List<VoteResult>?> GetResultsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<VoteResult>>("vote/results", _json); }
        catch { return null; }
    }

    // ── AUTH (teste manual) ────────────────────────────────────

    public async Task<(bool Authorized, string Reason)> AuthAsync(int fingerId)
    {
        try
        {
            var res  = await _http.PostAsJsonAsync("auth", new { fingerId });
            var body = await res.Content.ReadFromJsonAsync<AuthResponse>(_json);
            return (body?.Autorizado ?? false, body?.Motivo ?? "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── PING ──────────────────────────────────────────────────

    public async Task<bool> PingAsync()
    {
        try
        {
            var res = await _http.GetAsync("entities");
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── SERIAL BACKEND ──────────────────────────────────────

    public async Task<List<string>?> GetSerialPortsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<string>>("serial/ports", _json); }
        catch { return null; }
    }

    public async Task<SerialStatusDto?> GetSerialStatusAsync()
    {
        try { return await _http.GetFromJsonAsync<SerialStatusDto>("serial/status", _json); }
        catch { return null; }
    }

    public async Task<(bool Ok, string Message)> ConnectSerialAsync(string portName, int baudRate)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("serial/connect", new { portName, baudRate });
            var body = await res.Content.ReadFromJsonAsync<VoteActionResponse>(_json);
            return res.IsSuccessStatusCode
                ? (true, body?.Mensagem ?? "A conectar porta serial.")
                : (false, body?.Mensagem ?? $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Message)> DisconnectSerialAsync()
    {
        try
        {
            var res = await _http.PostAsync("serial/disconnect", null);
            var body = await res.Content.ReadFromJsonAsync<VoteActionResponse>(_json);
            return res.IsSuccessStatusCode
                ? (true, body?.Mensagem ?? "Porta serial desconectada.")
                : (false, body?.Mensagem ?? $"Erro {(int)res.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── Base URL ──────────────────────────────────────────────

    public void SetBaseUrl(string url)
    {
        if (!url.EndsWith('/')) url += '/';
        _http.BaseAddress = new Uri(url);
        Preferences.Set("ApiBaseUrl", url);
    }

    public string CurrentBaseUrl => _http.BaseAddress?.ToString() ?? "";
}

file record AuthResponse(bool Autorizado, string Motivo);
file record VoteInitiateResponse(bool Sucesso, string? NomeEleitor, string? Mensagem);
file record VoteScanResponse(bool Sucesso, int FingerId, string? NomeEleitor, string? Mensagem);
file record VoteActionResponse(bool Sucesso, string? Mensagem);
public record SerialStatusDto(bool IsConnected, string? ActivePort, string? DesiredPort, int BaudRate, string LastError);

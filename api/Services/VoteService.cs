using BeneditaApi.Data;
using BeneditaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BeneditaApi.Services;

public class VoteService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VoteService> _logger;

    public VoteService(AppDbContext db, ILogger<VoteService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────
    //  AUTH
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se o eleitor com <paramref name="fingerId"/> está
    /// cadastrado e ainda não votou.
    /// </summary>
    public async Task<(bool Authorized, string Reason)> AuthorizeAsync(int fingerId)
    {
        var voter = await _db.Voters
            .Include(v => v.Vote)
            .FirstOrDefaultAsync(v => v.FingerId == fingerId);

        if (voter is null)
            return (false, "Eleitor não cadastrado");

        if (!voter.CanVote)
            return (false, "Voto já realizado");

        if (voter.Vote is not null)
            return (false, "Voto já registrado");

        return (true, "OK");
    }

    // ──────────────────────────────────────────────────────────
    //  VOTE
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registra o voto do eleitor. Retorna false se já votou ou não existir.
    /// </summary>
    public async Task<(bool Success, string Message)> CastVoteAsync(int fingerId, string option)
    {
        var voter = await _db.Voters
            .Include(v => v.Vote)
            .FirstOrDefaultAsync(v => v.FingerId == fingerId);

        if (voter is null)
            return (false, "Eleitor não encontrado");

        if (voter.Vote is not null || !voter.CanVote)
            return (false, "Eleitor já votou");

        var vote = new Vote
        {
            FingerId = fingerId,
            Option   = option,
            VoterId  = voter.Id,
            CastAt   = DateTime.UtcNow
        };

        voter.CanVote = false;
        _db.Votes.Add(vote);

        await _db.SaveChangesAsync();
        _logger.LogInformation("Voto registado: finger={FingerId} opção={Option}", fingerId, option);
        return (true, "OK");
    }

    // ──────────────────────────────────────────────────────────
    //  VOTERS (CRUD)
    // ──────────────────────────────────────────────────────────

    public async Task<List<Voter>> GetAllVotersAsync() =>
        await _db.Voters.Include(v => v.Vote).ToListAsync();

    public async Task<Voter?> GetVoterByFingerAsync(int fingerId) =>
        await _db.Voters.Include(v => v.Vote).FirstOrDefaultAsync(v => v.FingerId == fingerId);

    public async Task<Voter> RegisterVoterAsync(int fingerId, string name)
    {
        if (await _db.Voters.AnyAsync(v => v.FingerId == fingerId))
            throw new InvalidOperationException($"FingerId {fingerId} já está cadastrado.");

        var voter = new Voter { FingerId = fingerId, Name = name };
        _db.Voters.Add(voter);
        await _db.SaveChangesAsync();
        return voter;
    }

    public async Task<bool> DeleteVoterAsync(int fingerId)
    {
        var voter = await _db.Voters.FirstOrDefaultAsync(v => v.FingerId == fingerId);
        if (voter is null) return false;
        _db.Voters.Remove(voter);
        await _db.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────────────────────────────────────
    //  RESULTS
    // ──────────────────────────────────────────────────────────

    public async Task<Dictionary<string, int>> GetResultsAsync()
    {
        var votes = await _db.Votes.ToListAsync();
        return votes
            .GroupBy(v => v.Option)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}

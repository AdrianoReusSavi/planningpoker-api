using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Classes;

namespace PlanningPoker.Api.Hubs;

public class PlanningHub : Hub
{
    private static ConcurrentDictionary<string, Room> _rooms = new();

    public async Task CriarSala(string salaId, string nome)
    {
        var sala = _rooms.GetOrAdd(salaId, _ => new Room { OwnerId = Context.ConnectionId });

        sala.Users[Context.ConnectionId] = nome;
        await Groups.AddToGroupAsync(Context.ConnectionId, salaId);
        await AtualizarUsers(salaId);

        await Clients.Group(salaId).SendAsync("AtualizarDono", sala.OwnerId);
    }

    public async Task EntrarSala(string salaId, string nome)
    {
        if (_rooms.TryGetValue(salaId, out var sala))
        {
            sala.Users[Context.ConnectionId] = nome;
            await Groups.AddToGroupAsync(Context.ConnectionId, salaId);
            await AtualizarUsers(salaId);

            await Clients.Group(salaId).SendAsync("AtualizarDono", sala.OwnerId);
        }
    }

    public async Task SairSala(string salaId, string nome)
    {
        if (_rooms.TryGetValue(salaId, out var sala))
        {
            sala.Users.Remove(Context.ConnectionId);
            sala.Votes.Remove(nome);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, salaId);
            await AtualizarUsers(salaId);

            if (sala.Users.Count == 0)
                _rooms.TryRemove(salaId, out _);
        }
    }

    public async Task EnviarVoto(string salaId, string voto)
    {
        if (_rooms.TryGetValue(salaId, out var sala) && sala.Users.TryGetValue(Context.ConnectionId, out var nomeUsuario))
        {
            sala.Votes[nomeUsuario] = voto;
            await Clients.Group(salaId).SendAsync("AtualizarVoto", nomeUsuario, voto);
        }
    }

    public async Task RevelarVotes(string salaId)
    {
        if (_rooms.TryGetValue(salaId, out var sala))
        {
            await Clients.Group(salaId).SendAsync("VotesRevelados", sala.Votes);
        }
    }

    public async Task ResetarVotes(string salaId)
    {
        if (_rooms.TryGetValue(salaId, out var sala) && sala.OwnerId == Context.ConnectionId)
        {
            sala.Votes.Clear();
            await Clients.Group(salaId).SendAsync("VotacaoResetada");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        foreach (var salaId in _rooms.Keys)
        {
            if (!_rooms.TryGetValue(salaId, out var sala) ||
                !sala.Users.Remove(Context.ConnectionId, out var nomeRemovido)) continue;

            sala.Votes.Remove(nomeRemovido);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, salaId);
            await AtualizarUsers(salaId);

            if (sala.Users.Count == 0)
                _rooms.TryRemove(salaId, out _);
        }

        await base.OnDisconnectedAsync(ex);
    }

    private Task AtualizarUsers(string salaId)
    {
        var nomes = _rooms[salaId].Users.Values.ToList();
        return Clients.Group(salaId).SendAsync("AtualizarUsuarios", nomes);
    }
}
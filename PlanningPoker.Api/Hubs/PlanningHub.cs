using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Classes;

namespace PlanningPoker.Api.Hubs;

public class PlanningHub : Hub
{
    private static readonly ConcurrentDictionary<string, Room> Rooms = new();

    public async Task CreateRoom(string name, string roomName)
    {
        var roomId = Guid.NewGuid().ToString();
        var room = Rooms.GetOrAdd(roomId,
            _ => new Room
            {
                RoomName = roomName,
                OwnerId = Context.ConnectionId
            });

        room.Users[Context.ConnectionId] = name;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await UpdateUsers(roomId);

        await Clients.Caller.SendAsync("RoomCreated", roomId, roomName);
        await Clients.Group(roomId).SendAsync("UpdateOwner", room.OwnerId);
    }

    public async Task EnterRoom(string roomId, string name)
    {
        if (Rooms.TryGetValue(roomId, out var room))
        {
            room.Users[Context.ConnectionId] = name;
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await UpdateUsers(roomId);

            await Clients.Caller.SendAsync("RoomJoined", roomId, room.RoomName);
            await Clients.Group(roomId).SendAsync("UpdateOwner", room.OwnerId);
        }
    }

    public async Task LeaveRoom(string roomId, string name)
    {
        if (Rooms.TryGetValue(roomId, out var room))
        {
            room.Users.Remove(Context.ConnectionId);
            room.Votes.Remove(name);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await UpdateUsers(roomId);

            if (room.Users.Count == 0)
                Rooms.TryRemove(roomId, out _);
        }
    }

    public async Task SendVote(string roomId, string vote)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.Users.TryGetValue(Context.ConnectionId, out var username))
        {
            room.Votes[username] = vote;
            await Clients.Group(roomId).SendAsync("UpdateVote", username, vote);
        }
    }

    public async Task RevealVotes(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Group(roomId).SendAsync("VotesRevealed", room.Votes);
        }
    }

    public async Task ResetVotes(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.OwnerId == Context.ConnectionId)
        {
            room.Votes.Clear();
            await Clients.Group(roomId).SendAsync("VotesReset");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        foreach (var salaId in Rooms.Keys)
        {
            if (!Rooms.TryGetValue(salaId, out var sala) ||
                !sala.Users.Remove(Context.ConnectionId, out var nomeRemovido)) continue;

            sala.Votes.Remove(nomeRemovido);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, salaId);
            await UpdateUsers(salaId);

            if (sala.Users.Count == 0)
                Rooms.TryRemove(salaId, out _);
        }

        await base.OnDisconnectedAsync(ex);
    }

    private Task UpdateUsers(string roomId)
    {
        var names = Rooms[roomId].Users.Values.ToList();
        return Clients.Group(roomId).SendAsync("UpdateUsers", names);
    }
}
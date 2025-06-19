using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Classes;

namespace PlanningPoker.Api.Hubs;

public class PlanningHub : Hub
{
    private static readonly ConcurrentDictionary<string, Room> Rooms = new();
    private static readonly ConcurrentDictionary<string, string> UserRooms = new();

    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong");
    }

    public async Task CreateRoom(string name, string roomName)
    {
        var roomId = Guid.NewGuid().ToString();
        var room = new Room { RoomName = roomName, OwnerId = Context.ConnectionId };
        Rooms.TryAdd(roomId, room);

        var success = await AddUserToRoom(roomId, name);
        await Clients.Caller.SendAsync("OnRoom", success);
    }

    public async Task EnterRoom(string roomId, string name)
    {
        var success = await AddUserToRoom(roomId, name);
        await Clients.Caller.SendAsync("OnRoom", success);
    }
    
    public async Task WatchRoom(string roomId, string name)
    {
        var success = await AddWatchRoom(roomId);
        await Clients.Caller.SendAsync("OnRoom", success);
    }

    public async Task GetRoomSettings()
    {
        if (UserRooms.TryGetValue(Context.ConnectionId, out var roomId) &&
            Rooms.TryGetValue(roomId, out var room))
        {
            var isLeader = room.OwnerId == Context.ConnectionId;
            var isWatching = room.Users.Select(s => s.ConnectionId).Contains(Context.ConnectionId);
            await Clients.Caller.SendAsync("RoomSettings", roomId, room.RoomName, isLeader, isWatching);
            await Clients.Group(roomId).SendAsync("GetGroup", room.Users);
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        await RemoveUserFromRoom(roomId);
    }

    public async Task SubmitVote(string roomId, string vote)
    {
        if (Rooms.TryGetValue(roomId, out var room))
        {
            var user = room.Users.FirstOrDefault(f => f.ConnectionId == Context.ConnectionId);

            if (user is not null)
            {
                user.Vote = vote;
                await Clients.Group(roomId).SendAsync("GetGroup", room.Users);
            }
        }
    }

    public async Task RevealVotes(string roomId)
    {
        await Clients.Group(roomId).SendAsync("VotesRevealed", true);
    }

    public async Task ResetVotes(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.OwnerId == Context.ConnectionId)
        {
            room.Users.ForEach(f => f.Vote = null);
            await Clients.Group(roomId).SendAsync("VotesRevealed", false);
            await Clients.Group(roomId).SendAsync("GetGroup", room.Users);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        if (UserRooms.TryGetValue(Context.ConnectionId, out var roomId))
            await RemoveUserFromRoom(roomId);

        await base.OnDisconnectedAsync(ex);
    }

    #region Privates

    private async Task<bool> AddUserToRoom(string roomId, string name)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
            return false;

        var user = new User
        {
            ConnectionId = Context.ConnectionId,
            Username = name,
        };

        room.Users.Add(user);
        UserRooms[Context.ConnectionId] = roomId;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        return true;
    }

    private async Task<bool> AddWatchRoom(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out _))
            return false;

        UserRooms[Context.ConnectionId] = roomId;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        return true;
    }

    private async Task RemoveUserFromRoom(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
            return;

        var user = room.Users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
        if (user is not null)
        {
            room.Users.Remove(user);

            if (room.OwnerId == user.ConnectionId && room.Users.Count > 0)
            {
                room.OwnerId = room.Users.FirstOrDefault()!.ConnectionId;
                await Clients.Client(room.OwnerId).SendAsync("RoomSettings", roomId, room.RoomName, true);
            }
        }

        UserRooms.TryRemove(Context.ConnectionId, out _);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await Clients.Caller.SendAsync("OnRoom", false);
        await Clients.Group(roomId).SendAsync("GetGroup", room.Users);

        if (room.Users.Count == 0)
            Rooms.TryRemove(roomId, out _);
    }

    #endregion
}
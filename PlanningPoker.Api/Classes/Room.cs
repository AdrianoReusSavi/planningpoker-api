namespace PlanningPoker.Api.Classes;

public class Room
{
    public required string OwnerId  { get; set; }
    public required string RoomName { get; set; }
    public List<User> Users { get; set; } = [];
}
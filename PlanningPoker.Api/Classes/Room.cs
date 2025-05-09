namespace PlanningPoker.Api.Classes;

public class Room
{
    public required string OwnerId  { get; set; }
    public Dictionary<string, string> Users { get; set; } = new();
    public Dictionary<string, string> Votes { get; set; } = new();
}
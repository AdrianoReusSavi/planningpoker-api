namespace PlanningPoker.Api.Classes;

public class User
{
    public required string ConnectionId { get; set; }
    public required string Username { get; set; }
    public string? Vote { get; set; }
}
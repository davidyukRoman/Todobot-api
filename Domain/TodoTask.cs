namespace Domain;

public class TodoTask
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
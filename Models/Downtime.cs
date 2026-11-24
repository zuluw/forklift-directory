namespace ForkliftDirectory.Models;

public class Downtime
{
    public int Id { get; set; }

    public int ForkliftId { get; set; }

    public Forklift Forklift { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string Description { get; set; } = string.Empty;
}
namespace ForkliftDirectory.Models;

public class Forklift
{
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public decimal Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public ICollection<Downtime> Downtimes { get; set; } = new List<Downtime>();
}
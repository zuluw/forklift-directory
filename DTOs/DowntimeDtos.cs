using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations;

namespace ForkliftDirectory.DTOs;

public sealed class DowntimeResponseDto
{
    public int Id { get; set; }

    public int ForkliftId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public bool IsActive { get; set; }

    public long DurationMinutes { get; set; }

    public string DurationFormatted { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class CreateDowntimeRequestDto
{
    [Required(ErrorMessage = "Дата и время начала обязательны для заполнения.")]
    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required(ErrorMessage = "Описание инцидента обязательно для заполнения.")]
    [StringLength(2000, ErrorMessage = "Длина описания инцидента не должна превышать 2000 символов.")]
    public string Description { get; set; } = string.Empty;
}

public sealed class UpdateDowntimeRequestDto
{
    [Required(ErrorMessage = "Дата и время начала обязательны для заполнения.")]
    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required(ErrorMessage = "Описание инцидента обязательно для заполнения.")]
    [StringLength(2000, ErrorMessage = "Длина описания инцидента не должна превышать 2000 символов.")]
    public string Description { get; set; } = string.Empty;
}
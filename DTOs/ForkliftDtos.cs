using System.ComponentModel.DataAnnotations;

namespace ForkliftDirectory.DTOs;

public sealed class ForkliftResponseDto
{
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public decimal Capacity { get; set; }

    public bool IsActive { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public int DowntimesCount { get; set; }
}

public sealed class CreateForkliftRequestDto
{
    [Required(ErrorMessage = "Поле «Марка» обязательно для заполнения.")]
    [StringLength(150, ErrorMessage = "Длина поля «Марка» не должна превышать 150 символов.")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Поле «Номер» обязательно для заполнения.")]
    [StringLength(100, ErrorMessage = "Длина поля «Номер» не должна превышать 100 символов.")]
    public string Number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Поле «Грузоподъемность» обязательно для заполнения.")]
    public decimal? Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(150, ErrorMessage = "Длина поля «Пользователь» не должна превышать 150 символов.")]
    public string? UpdatedBy { get; set; }
}

public sealed class UpdateForkliftRequestDto
{
    [Required(ErrorMessage = "Поле «Марка» обязательно для заполнения.")]
    [StringLength(150, ErrorMessage = "Длина поля «Марка» не должна превышать 150 символов.")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Поле «Номер» обязательно для заполнения.")]
    [StringLength(100, ErrorMessage = "Длина поля «Номер» не должна превышать 100 символов.")]
    public string Number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Поле «Грузоподъемность» обязательно для заполнения.")]
    public decimal? Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(150, ErrorMessage = "Длина поля «Пользователь» не должна превышать 150 символов.")]
    public string? UpdatedBy { get; set; }
}
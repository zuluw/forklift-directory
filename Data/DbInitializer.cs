using System.Net.Sockets;
using ForkliftDirectory.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ForkliftDirectory.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DbInitializer");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const int maxRetries = 5;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await dbContext.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientDbException(ex))
            {
                logger.LogWarning(
                    ex,
                    "База данных еще не готова (попытка {Attempt} из {MaxRetries}). Повтор через 2 сек...",
                    attempt,
                    maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        if (await dbContext.Forklifts.AnyAsync())
        {
            return;
        }

        var amkodor = new Forklift
        {
            Brand = "Амкодор",
            Number = "4565 PH-1",
            Capacity = 2.5m,
            IsActive = true,
            UpdatedAt = new DateTime(2024, 4, 16, 14, 12, 0, DateTimeKind.Utc),
            UpdatedBy = "Иванов И.И."
        };

        var toyota = new Forklift
        {
            Brand = "Toyota",
            Number = "8821 AB-7",
            Capacity = 3.0m,
            IsActive = true,
            UpdatedAt = new DateTime(2024, 5, 4, 10, 30, 0, DateTimeKind.Utc),
            UpdatedBy = "Петров П.П."
        };

        dbContext.Forklifts.AddRange(amkodor, toyota);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var activeStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc)
            .AddMinutes(-45);

        var downtimes = new List<Downtime>
        {
            new()
            {
                ForkliftId = amkodor.Id,
                StartTime = new DateTime(2024, 4, 16, 14, 12, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 4, 16, 15, 47, 0, DateTimeKind.Utc),
                Description = "сломался"
            },
            new()
            {
                ForkliftId = amkodor.Id,
                StartTime = activeStart,
                EndTime = null,
                Description = "Утечка масла в гидравлическом контуре (требуется осмотр)"
            }
        };

        dbContext.Downtimes.AddRange(downtimes);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Начальные тестовые данные успешно добавлены в базу данных.");
    }

    private static bool IsTransientDbException(Exception ex)
    {
        if (ex is NpgsqlException || ex is SocketException || ex is TimeoutException)
        {
            return true;
        }

        return ex.InnerException is not null && IsTransientDbException(ex.InnerException);
    }
}
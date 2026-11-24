using ForkliftDirectory.Data;
using ForkliftDirectory.DTOs;
using ForkliftDirectory.Middleware;
using ForkliftDirectory.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in context.ModelState)
            {
                if (entry.Value.Errors.Count == 0)
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(entry.Key)
                    ? "request"
                    : entry.Key.TrimStart('$', '.');

                var messages = entry.Value.Errors
                    .Select(e => NormalizeModelErrorMessage(e.ErrorMessage))
                    .Distinct()
                    .ToArray();

                errors[key] = messages;
            }

            var firstMessage = errors.Values.SelectMany(v => v).FirstOrDefault()
                ?? "Проверьте корректность заполнения полей.";

            return new BadRequestObjectResult(new ErrorResponse(firstMessage, "VALIDATION_ERROR", errors));
        };
    });

builder.Services.AddOpenApi();

var connectionString = BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IForkliftService, ForkliftService>();
builder.Services.AddScoped<IDowntimeService, DowntimeService>();

var app = builder.Build();

if (!EF.IsDesignTime)
{
    await DbInitializer.InitializeAsync(app.Services);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

await app.RunAsync();

static string BuildConnectionString(IConfiguration configuration)
{
    var envDefaultConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (!string.IsNullOrWhiteSpace(envDefaultConn))
    {
        return envDefaultConn;
    }

    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);

            var npgsqlBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
            };

            var sslModeConfigured = false;
            if (!string.IsNullOrWhiteSpace(uri.Query))
            {
                var queryPairs = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in queryPairs)
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Enum.TryParse<SslMode>(kv[1], ignoreCase: true, out var sslMode))
                        {
                            npgsqlBuilder.SslMode = sslMode;
                            sslModeConfigured = true;
                        }
                    }
                }
            }

            if (!sslModeConfigured &&
                !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("postgres", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("db", StringComparison.OrdinalIgnoreCase))
            {
                npgsqlBuilder.SslMode = SslMode.Prefer;
            }

            return npgsqlBuilder.ConnectionString;
        }

        return databaseUrl;
    }

    var connStr = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connStr))
    {
        throw new InvalidOperationException(
            "Строка подключения к БД не задана. Укажите ConnectionStrings__DefaultConnection или DATABASE_URL.");
    }

    return connStr;
}

static string NormalizeModelErrorMessage(string? message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return "Некорректное значение поля.";
    }

    if (message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("is not valid", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("A non-empty request body is required", StringComparison.OrdinalIgnoreCase))
    {
        return "Некорректный формат значения поля.";
    }

    return message;
}
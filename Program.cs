using ChillingAddrManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);


var botToken = builder.Configuration["BotConfiguration:BotToken"];
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine("Lỗi: Không tìm thấy BotToken trong appsettings.json");
    return;
}

// 2. Đăng ký (Register) TelegramBotClient với
// hệ thống Dependency Injection (DI) của ASP.NET Core.
builder.Services.AddHttpClient("telegram_bot_client")
        .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
        {
            return new TelegramBotClient(botToken, httpClient);
        });

builder.Services.AddSingleton<GoogleSheetService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<BotUpdateHandler>();

var app = builder.Build();

// 3. TẠO WEBHOOK ENDPOINT
// Ví dụ: https://ten-mien-cua-ban.com/api/webhook
app.MapPost("/api/webhook", async (
    HttpContext context,
    BotUpdateHandler botHandler) =>
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                },
                Converters = new List<Newtonsoft.Json.JsonConverter> {
                    new UnixDateTimeConverter(),
                    new StringEnumConverter(new SnakeCaseNamingStrategy())
                }
            };

            var update = JsonConvert.DeserializeObject<Update>(body, settings);

            if (update == null) return Results.Ok();

            await botHandler.HandleUpdateAsync(update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LỖI SYSTEM: {ex.Message}");
        }

        return Results.Ok();
    });

app.Run();
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});


// 1. Đọc Token từ appsettings.json
var botToken = builder.Configuration["BotConfiguration:BotToken"];
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine("Lỗi: Không tìm thấy BotToken trong appsettings.json");
    return;
}

// 2. Đăng ký (Register) TelegramBotClient với
// hệ thống Dependency Injection (DI) của ASP.NET Core.
// Điều này cho phép chúng ta "tiêm" client vào endpoint.
builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
    {
        return new TelegramBotClient(botToken, httpClient);
    });

var app = builder.Build();

// 3. TẠO WEBHOOK ENDPOINT
// Đây là URL mà Telegram sẽ gọi mỗi khi có tin nhắn
// Ví dụ: https://ten-mien-cua-ban.com/api/webhook
app.MapPost("/api/webhook", async (
    ITelegramBotClient botClient, // Lấy bot client đã đăng ký ở trên
    [FromBody] Update update,     // Lấy nội dung (JSON) mà Telegram gửi
    CancellationToken cancellationToken) =>
{
    // 4. Kiểm tra xem có phải là tin nhắn text không
    if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
    {
        var message = update.Message;
        var chatId = message.Chat.Id;
        var messageText = message.Text;

        Console.WriteLine($"Received message from {chatId}: '{messageText}'");

        // 5. GỬI TIN NHẮN TRẢ LỜI (ECHO)
        // Gửi lại chính nội dung tin nhắn đó
        await botClient.SendMessage(
            chatId: chatId,
            text: $"Bạn vừa nói: {messageText}", // Đây là nội dung bot trả lời
            cancellationToken: cancellationToken
        );
    }

    // Báo cho Telegram biết là đã nhận OK
    return Results.Ok();
});

app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var builder = WebApplication.CreateSlimBuilder(args);

//builder.Services.ConfigureHttpJsonOptions(options =>
//{
//    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
//});


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
app.MapPost("/api/webhook", async (HttpContext context, ITelegramBotClient botClient) =>
{
    // 1. Đọc dữ liệu thô (string) từ Telegram gửi đến
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var settings = new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };
    // 2. Dùng Newtonsoft để dịch chuỗi JSON sang object Update
    // Cách này bỏ qua lỗi "TypeInfoResolver" bạn đang gặp
    var update = JsonConvert.DeserializeObject<Update>(body, settings);

    // Kiểm tra Message (Chat riêng / Group)
    if (update.Message != null && update.Message.Text != null)
    {
        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text;
        Console.WriteLine($"User chat: {text}");

        // Xử lý logic tra cứu...
        await botClient.SendMessage(chatId, $"Bot trả lời user: {text}");
    }
    // Kiểm tra ChannelPost (Tin nhắn từ Kênh)
    else if (update.ChannelPost != null && update.ChannelPost.Text != null)
    {
        var chatId = update.ChannelPost.Chat.Id;
        var text = update.ChannelPost.Text;
        Console.WriteLine($"Channel post: {text}");

        // Xử lý logic tra cứu...
        // Lưu ý: Bot phải là Admin trong channel mới chat được vào channel
        await botClient.SendMessage(chatId, $"Bot trả lời channel: {text}");
    }
    else
    {
        Console.WriteLine("Loại tin nhắn chưa hỗ trợ hoặc không có nội dung text.");
    }

    //// 3. Xử lý logic bot như cũ
    //if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
    //{
    //    var message = update.Message;
    //    Console.WriteLine($"Nhận tin nhắn: {message.Text}");

    //    await botClient.SendMessage(
    //        chatId: message.Chat.Id,
    //        text: $"Bạn vừa nói: {message.Text}"
    //    );
    //}

    return Results.Ok();
});

app.Run();

//public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

//[JsonSerializable(typeof(Todo[]))]
//internal partial class AppJsonSerializerContext : JsonSerializerContext
//{

//}

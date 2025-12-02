using ChillingAddrManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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

var app = builder.Build();

// 3. TẠO WEBHOOK ENDPOINT
// Đây là URL mà Telegram sẽ gọi mỗi khi có tin nhắn
// Ví dụ: https://ten-mien-cua-ban.com/api/webhook
app.MapPost("/api/webhook", async (HttpContext context, ITelegramBotClient botClient, GoogleSheetService sheetService) =>
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
            Converters = new List<Newtonsoft.Json.JsonConverter>
            {
                new UnixDateTimeConverter(),
                new StringEnumConverter(new SnakeCaseNamingStrategy())
            }
        };

        var update = JsonConvert.DeserializeObject<Update>(body, settings);

        if (update == null) return Results.Ok();

        // Lấy tin nhắn (từ User hoặc Channel)
        var message = update.Message ?? update.ChannelPost;

        if (message != null && !string.IsNullOrEmpty(message.Text))
        {
            var chatId = message.Chat.Id;
            var userText = message.Text.Trim(); // Không tolower vội, để giữ hoa thường khi nhập
            Console.WriteLine($"Nhận yêu cầu: {userText}");

            // --- LOGIC 1: THÊM MỚI (Nếu bắt đầu bằng /add) ---
            if (userText.ToLower().StartsWith("/add"))
            {
                try
                {
                    // Cắt chuỗi lệnh "/add" đi, chỉ lấy phần nội dung
                    var content = userText.Substring(4).Trim();

                    // Tách các trường bằng dấu gạch đứng |
                    var parts = content.Split('|');

                    if (parts.Length < 3) // Yêu cầu tối thiểu phải có Tên, Loại, Danh mục
                    {
                        await botClient.SendMessage(chatId,
                            "⚠️ Sai cú pháp! Hãy nhập:\n/add Tên | Loại | Danh mục | Địa chỉ | TP | Note");
                    }
                    else
                    {
                        // Tạo object mới
                        var newNote = new LocationNote
                        {
                            Name = parts[0].Trim(),
                            Type = parts.Length > 1 ? parts[1].Trim() : "",
                            Category = parts.Length > 2 ? parts[2].Trim() : "",
                            Address = parts.Length > 3 ? parts[3].Trim() : "",
                            City = parts.Length > 4 ? parts[4].Trim() : "",
                            Note = parts.Length > 5 ? parts[5].Trim() : ""
                        };

                        // Gọi hàm lưu vào Google Sheet
                        await sheetService.AddRowAsync(newNote);

                        await botClient.SendMessage(chatId,
                            $"✅ Đã thêm thành công:\n🏠 {newNote.Name}\n📂 {newNote.Category}");
                    }
                }
                catch (Exception ex)
                {
                    await botClient.SendMessage(chatId, $"❌ Lỗi khi thêm: {ex.Message}");
                }
            }
            else
            {
                var searchText = userText.ToLower();
                // 1. Lấy toàn bộ dữ liệu từ Google Sheet
                var allData = await sheetService.GetDataAsync();
                string responseText = "";

                // LOGIC 1: Tìm chính xác theo Tên (Name) hoặc Loại (Type) -> Trả về chi tiết
                var matchItem = allData.FirstOrDefault(x =>
                    x.Name.ToLower().Contains(searchText) ||
                    x.Type.ToLower() == searchText);

                if (matchItem != null && !allData.Any(x => x.Category.ToLower() == searchText))
                {
                    // Nếu tìm thấy item cụ thể (và user không chat trùng tên Category)
                    responseText = matchItem.ToDetailString();
                }
                else
                {
                    // LOGIC 2: Tìm theo Category (food, chill...) -> Trả về danh sách
                    var byCategory = allData.Where(x => x.Category.ToLower().Contains(searchText)).ToList();
                    if (byCategory.Count > 0)
                    {
                        responseText = $"📂 **Danh mục: {searchText}**\n";
                        foreach (var item in byCategory)
                        {
                            responseText += $"- {item.Name} ({item.Type}) - {item.Address}\n";
                        }
                    }
                    else
                    {
                        // LOGIC 3: Tìm theo Thành phố (City) hoặc Địa chỉ -> Trả về danh sách
                        var byPlace = allData.Where(x =>
                            x.City.ToLower().Contains(searchText) ||
                            x.Address.ToLower().Contains(searchText)).ToList();

                        if (byPlace.Count > 0)
                        {
                            responseText = $"📍 **Tại: {searchText}**\n";
                            foreach (var item in byPlace)
                            {
                                responseText += $"- {item.Name} ({item.Category})\n";
                            }
                        }
                    }
                }

                // Nếu không tìm thấy gì hết
                if (string.IsNullOrEmpty(responseText))
                {
                    responseText = "Không tìm thấy thông tin phù hợp. Thử tìm 'food', 'hà nội' hoặc tên quán xem sao!";
                }

                // Gửi kết quả về Telegram (ParseMode Markdown để in đậm)
                await botClient.SendMessage(chatId, responseText, parseMode: ParseMode.Markdown);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"LỖI: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }

    return Results.Ok();
});

app.Run();
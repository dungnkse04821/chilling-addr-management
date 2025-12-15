using ChillingAddrManagement;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class BotUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly GoogleSheetService _sheetService;
    private readonly IMemoryCache _cache;

    public BotUpdateHandler(ITelegramBotClient botClient, GoogleSheetService sheetService, IMemoryCache cache)
    {
        _botClient = botClient;
        _sheetService = sheetService;
        _cache = cache;
    }

    public async Task HandleUpdateAsync(Update update)
    {
        var message = update.Message ?? update.ChannelPost;

        if (message == null || string.IsNullOrEmpty(message.Text)) return;

        var chatId = message.Chat.Id;
        var userText = message.Text.Trim();
        string cacheKey = $"User_{chatId}";

        // 2. Xử lý lệnh HỦY
        if (userText == "/cancel")
        {
            _cache.Remove(cacheKey);
            await _botClient.SendMessage(chatId, "🚫 Đã hủy nhập liệu.");
            return;
        }

        if (!_cache.TryGetValue(cacheKey, out UserSession session))
        {
            session = new UserSession();
        }

        switch (session.Step)
        {
            case "NONE":
                if (userText == "/add")
                {
                    session.Step = "WAITING_NAME";
                    _cache.Set(cacheKey, session);
                    await _botClient.SendMessage(chatId, "📝 <b>Nhập tên quán/hoạt động:</b>\n(Gõ /cancel để hủy)", parseMode: ParseMode.Html);
                }
                else
                {
                    await HandleSearchAsync(chatId, userText);
                }
                break;

            case "WAITING_NAME":
                session.DraftData.Name = userText;
                session.Step = "WAITING_TYPE";
                _cache.Set(cacheKey, session);
                await _botClient.SendMessage(chatId, "🍜 <b>Loại là gì?</b> (VD: Phở, Cafe...):", parseMode: ParseMode.Html);
                break;

            case "WAITING_TYPE":
                session.DraftData.Type = userText;
                session.Step = "WAITING_CATEGORY";
                _cache.Set(cacheKey, session);
                await _botClient.SendMessage(chatId, "📂 <b>Danh mục (Category)?</b> (VD: food, chill):", parseMode: ParseMode.Html);
                break;

            case "WAITING_CATEGORY":
                session.DraftData.Category = userText;
                session.Step = "WAITING_ADDRESS";
                _cache.Set(cacheKey, session);
                await _botClient.SendMessage(chatId, "📍 <b>Địa chỉ?</b> (Nhập 'k' để bỏ qua):", parseMode: ParseMode.Html);
                break;

            case "WAITING_ADDRESS":
                session.DraftData.Address = userText == "k" ? "" : userText;
                session.Step = "WAITING_CITY";
                _cache.Set(cacheKey, session);
                await _botClient.SendMessage(chatId, "🏙 <b>Thành phố?</b>:", parseMode: ParseMode.Html);
                break;

            case "WAITING_CITY":
                session.DraftData.City = userText;
                session.Step = "WAITING_NOTE";
                _cache.Set(cacheKey, session);
                await _botClient.SendMessage(chatId, "📝 <b>Ghi chú?</b> (Nhập 'k' để bỏ qua):", parseMode: ParseMode.Html);
                break;

            case "WAITING_NOTE":
                session.DraftData.Note = userText == "k" ? "" : userText;

                await _botClient.SendMessage(chatId, "⏳ Đang lưu vào Google Sheet...");

                // Gọi service lưu data
                await _sheetService.AddRowAsync(session.DraftData);

                _cache.Remove(cacheKey);
                await _botClient.SendMessage(chatId,
                    $"✅ <b>Đã lưu thành công!</b>\n🏠 {session.DraftData.Name}",
                    parseMode: ParseMode.Html);
                break;
        }
    }

    private async Task HandleSearchAsync(long chatId, string keyword)
    {
        var allData = await _sheetService.GetDataAsync();
        string responseText = "";
        keyword = keyword.ToLower();

        var findExact = keyword.Split(' ')[0];

        string FormatResult(IEnumerable<LocationNote> items)
        {
            if (!items.Any()) return "📭 Không tìm thấy kết quả nào theo tiêu chí này.";

            return string.Join(
                Environment.NewLine + "---------------------------" + Environment.NewLine,
                items.Select(x => x.ToDetailString())
            );
        }

        IEnumerable<LocationNote> searchResult;

        switch (findExact)
        {
            // --- Tìm theo TÊN ---
            case "/name":
            case "/ten":
                searchResult = allData.Where(x => x.Name.ToLower().Contains(keyword));
                responseText = FormatResult(searchResult);
                break;

            // --- Tìm theo THÀNH PHỐ ---
            case "/city":
            case "/tp":
                searchResult = allData.Where(x => x.City.ToLower().Contains(keyword));
                responseText = FormatResult(searchResult);
                break;

            // --- Tìm theo LOẠI (Phở, Camping, Cafe...) ---
            case "/type":
            case "/loai":
                searchResult = allData.Where(x => x.Type.ToLower().Contains(keyword));
                responseText = FormatResult(searchResult);
                break;

            // --- Tìm theo DANH MỤC (Food, Chill...) ---
            case "/cate":
            case "/category":
            case "/danhmuc":
                searchResult = allData.Where(x => x.Category.ToLower().Contains(keyword));
                responseText = FormatResult(searchResult);
                break;

            // --- Tìm theo ĐỊA CHỈ ---
            case "/addr":
            case "/address":
            case "/diachi":
                searchResult = allData.Where(x => x.Address.ToLower().Contains(keyword));
                responseText = FormatResult(searchResult);
                break;

            // --- Tìm theo GHI CHÚ ---
            case "/note":
            case "/ghichu":
                searchResult = allData.Where(x => x.Note.ToLower().Contains(keyword));
                responseText = FormatResult(searchResult);
                break;

            // --- TÌM KIẾM THÔNG MINH (Mặc định khi không có lệnh) ---
            default:
                // Ưu tiên 1: Tìm theo Tên hoặc Loại
                searchResult = allData.Where(x => x.Name.ToLower().Contains(keyword) || x.Type.ToLower().Contains(keyword));

                if (searchResult.Any())
                {
                    responseText = FormatResult(searchResult);
                }
                else
                {
                    // Ưu tiên 2: Nếu không thấy tên/loại thì tìm theo Danh mục
                    var listByCategory = allData.Where(x => x.Category.ToLower().Contains(keyword)).ToList();

                    if (listByCategory.Any())
                    {
                        // Với danh mục, chỉ hiện tóm tắt danh sách để đỡ dài
                        responseText = $"📂 **Tìm thấy {listByCategory.Count} thông tin thuộc nhóm '{keyword}':**\n\n";
                        foreach (var item in listByCategory)
                        {
                            responseText += $"- {item.Name} ({item.Type}) - {item.City}\n";
                        }
                    }
                    else
                    {
                        responseText = "❌ Không tìm thấy thông tin phù hợp. Hãy thử tìm theo tên món, hoặc dùng lệnh /city, /type...";
                    }
                }
                break;
        }

        await _botClient.SendMessage(chatId, responseText, parseMode: ParseMode.Markdown);
    }
}
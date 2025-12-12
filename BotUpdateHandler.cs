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
        IEnumerable<LocationNote>? matchItem = null;
        switch (findExact)
        {
            case "/city":
                matchItem = allData.Where(x => x.City.ToLower().Contains(keyword));
                if (matchItem.Any())
                {
                    responseText = string.Join(Environment.NewLine + "---------------------------" + Environment.NewLine, matchItem.Select(x => x.ToDetailString()).ToArray());
                }
                break;
            default:
                matchItem = allData.Where(x => x.Name.ToLower().Contains(keyword) || x.Type.ToLower().Contains(keyword));
                if (matchItem.Any())
                {
                    responseText = string.Join(Environment.NewLine + "---------------------------" + Environment.NewLine, matchItem.Select(x => x.ToDetailString()).ToArray());
                }
                else
                {
                    var list = allData.Where(x => x.Category.ToLower().Contains(keyword)).ToList();
                    if (list.Any()) responseText = $"Tìm thấy {list.Count} quán thuộc nhóm {keyword}";
                    else responseText = "Không tìm thấy thông tin phù hợp.";
                }
                break;
        }


        await _botClient.SendMessage(chatId, responseText, parseMode: ParseMode.Markdown);
    }
}
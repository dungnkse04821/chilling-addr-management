namespace ChillingAddrManagement
{
    // Lưu trạng thái của cuộc hội thoại
    public class UserSession
    {
        public string Step { get; set; } = "NONE"; // Bước hiện tại (NAME, TYPE, ADDRESS...)
        public LocationNote DraftData { get; set; } = new LocationNote(); // Dữ liệu đang nhập dở
    }
}

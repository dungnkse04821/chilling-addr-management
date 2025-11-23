namespace ChillingAddrManagement
{
    public class LocationNote
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Note { get; set; } = "";

        // Hàm tiện ích để hiển thị thông tin chi tiết
        public string ToDetailString()
        {
            return $"🏠 Tên: **{Name}**\n" +
                   $"🏷 Loại: {Type}\n" +
                   $"📂 Danh mục: {Category}\n" +
                   $"📍 Địa chỉ: {Address}, {City}\n" +
                   $"📝 Ghi chú: {Note}";
        }
    }
}

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace ChillingAddrManagement
{
    public class GoogleSheetService
    {
        // ID của bảng tính (Lấy trên thanh địa chỉ trình duyệt: /d/ID_CUA_BAN/edit)
        private const string SpreadsheetId = "1Qu7HqRChsgBB6VU2FC-XGKAfzCLDRTV1FEB4gIUlg7A";

        // Tên Sheet (Tab) trong bảng tính
        private const string SheetName = "Sheet1";

        // Đường dẫn đến file JSON Service Account
        private const string PathToServiceAccountKey = "service_account.json";

        public async Task<List<LocationNote>> GetDataAsync()
        {
            // 1. Xác thực
            GoogleCredential credential;
            using (var stream = new FileStream(PathToServiceAccountKey, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
            }

            // 2. Tạo service
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "TelegramBot"
            });

            // 3. Đọc dữ liệu (Bỏ qua hàng đầu tiên là tiêu đề A1:F1)
            var range = $"{SheetName}!A2:F";
            var request = service.Spreadsheets.Values.Get(SpreadsheetId, range);
            var response = await request.ExecuteAsync();
            var values = response.Values;

            var results = new List<LocationNote>();

            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    // Map dữ liệu từ Row vào Object (Đề phòng row thiếu cột)
                    var note = new LocationNote
                    {
                        Name = row.Count > 0 ? row[0].ToString() : "",
                        Type = row.Count > 1 ? row[1].ToString() : "",
                        Category = row.Count > 2 ? row[2].ToString() : "",
                        Address = row.Count > 3 ? row[3].ToString() : "",
                        City = row.Count > 4 ? row[4].ToString() : "",
                        Note = row.Count > 5 ? row[5].ToString() : ""
                    };
                    results.Add(note);
                }
            }

            return results;
        }
    }
}

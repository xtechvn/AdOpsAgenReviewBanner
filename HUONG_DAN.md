# AdOps Agent Review Banner — Hướng dẫn

Ứng dụng console .NET 8 dùng **Gemini API** để review banner quảng cáo (hình ảnh) và trả về một trong hai kết quả: **`Blocked`** hoặc **`Reviewed`**.

---

## Mục tiêu

- Tự động phân loại banner theo tiêu chí Ad Ops (cờ bạc, nội dung người lớn, lừa đảo, v.v.).
- Kiến trúc **SOLID**, dễ mở rộng: đổi nguồn policy (API), đổi model vision, thêm API HTTP sau này.
- Cấu hình tiêu chí block qua **`appsettings.json`** (sau này có thể thay bằng API).

---

## Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- API key Gemini: [Google AI Studio](https://aistudio.google.com/apikey)

---

## Bắt đầu từ đâu? (đọc code / debug)

| Câu hỏi | Trả lời |
|---------|---------|
| **File chính?** | `Program.cs` — entry point, app chạy từ đây |
| **Đăng ký service?** | `DependencyInjection/ServiceCollectionExtensions.cs` |
| **Review 1 ảnh?** | `Application/ReviewBannerUseCase.cs` → `ExecuteAsync` |
| **Nghe RabbitMQ?** | `Infrastructure/Messaging/RabbitMqReviewConsumerService.cs` (chỉ Production) |
| **Mở link GAM?** | `Infrastructure/Selenium/SeleniumLinkImageFetcher.cs` |

```
Program.cs
    ├─ Test  → ReviewBannerBatchRunner (folder) hoặc ReviewBannerUseCase (1 file)
    │          hoặc ReviewQueueMessageProcessor (URL)
    └─ Production → host.RunAsync() → RabbitMqReviewConsumerService
                              → ReviewQueueMessageProcessor → UseCase
```

### Debug TEST trong Visual Studio / Cursor

1. Mở `appsettings.json` → `"Runtime": { "Environment": "Test" }`
2. Mở `Program.cs` → đặt **breakpoint** trong khối `TEST mode` (sau dòng `Console.WriteLine($"TEST mode...`)`)
3. Chọn profile debug ở `Properties/launchSettings.json`:
   - **TEST - Quét folder image_test** (mặc định)
   - **TEST - 1 file ảnh**
   - **TEST - Link GAM (Selenium)**
4. Nhấn **F5** (Start Debugging)

Hoặc terminal:

```powershell
cd D:\PROJECT\AdOpsAgenReviewBanner
dotnet run                                          # folder image_test
dotnet run -- "image_test\Screenshot_3.png"         # 1 ảnh — dễ debug nhất
dotnet run -- "https://admanager.google.com/..."    # test Selenium + queue flow
```

Breakpoint gợi ý: `ReviewBannerUseCase.ExecuteAsync` (bước gọi Gemini).

---

## Cài đặt và chạy

```powershell
cd F:\PROJECT\OCR_DETECT_BANNER_GAM\AdOpsAgenReviewBanner

# (Tùy chọn) Ghi đè API key trong appsettings
# $env:GEMINI_API_KEY = "your-api-key-here"

# Quét tất cả ảnh trong folder image_test (mặc định)
dotnet run

# Quét folder khác
dotnet run -- "image_test"
dotnet run -- "D:\banners\incoming"

# Review một ảnh đơn
dotnet run -- "D:\banners\banner-001.png"

# TEST: review qua link GAM (Selenium chụp màn hình, không cần RabbitMQ)
dotnet run -- "https://admanager.google.com/..."
```

**Kết quả stdout:** một dòng `Blocked` hoặc `Reviewed`.

---

## Môi trường TEST / PRODUCTION

| File | Mục đích |
|------|----------|
| `appsettings.json` | Local: `Runtime.Environment = Test`, quét folder / file / URL |
| `appsettings.Production.json` | Server: RabbitMQ + Selenium + worker **Reviewed** |
| `appsettings.Production.Blocked.json` | Override `WorkerMode = Blocked` (instance thứ 2) |

### Chạy local (TEST — không cần RabbitMQ)

`appsettings.json`:

```json
"Runtime": {
  "Environment": "Test",
  "WorkerMode": "Reviewed",
  "DefaultImageFolder": "image_test"
}
```

```powershell
dotnet run
dotnet run -- "https://link-gam-preview..."
```

### Chạy server (PRODUCTION — consumer RabbitMQ)

1. Tạo queue trên RabbitMQ (ví dụ `PROCESS_REVIEW_BANNER_DFP`, durable).
2. Publisher gửi JSON:

```json
{
  "link_review": "https://admanager.google.com/...",
  "mode": "reviewed"
}
```

`mode`: `reviewed` hoặc `blocked` — worker chỉ xử lý message **trùng** `Runtime.WorkerMode`.

3. Chạy worker **Reviewed**:

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run
# hoặc sau publish:
# dotnet AdOpsAgenReviewBanner.dll
```

4. Chạy worker **Blocked** (process riêng):

```powershell
$env:DOTNET_ENVIRONMENT = "ProductionBlocked"
dotnet run
```

### MongoDB (`Mongo`)

Map từ `CONSUMMER_SIS_REPORT/App.config`:

| App.config (cũ) | appsettings.json (mới) |
|-----------------|------------------------|
| `ConnectionMongo` | `Mongo:ConnectionString` |
| `MongoCatalog` | `Mongo:Database` |
| (collection riêng) | `Mongo:CollectionBannerReview` |

```json
"Mongo": {
  "Enabled": false,
  "ConnectionString": "mongodb://localhost:27017",
  "Database": "news_fptonline",
  "CollectionBannerReview": "banner_review_log"
}
```

- **Local TEST:** `Enabled: false`, `mongodb://localhost:27017`
- **Production:** `appsettings.Production.json` dùng server `10.1.11.148` (có auth), `Enabled: true`
- Ghi đè: `Mongo__ConnectionString`, `Mongo__Database`, ...

Kết nối trong code (khi triển khai repository) giống project mẫu:

```csharp
var client = new MongoClient(connectionString);
var db = client.GetDatabase(database);
var collection = db.GetCollection<T>(collectionName);
```

### Cấu hình Production (`appsettings.Production.json`)

Đã map từ `AppAutoSubmitBannerDFP` (`App.config`):

```json
"RabbitMq": {
  "HostName": "180.148.142.129",
  "VirtualHost": "polyad",
  "UserName": "admin",
  "Password": "admin",
  "Port": 5672,
  "QueueName": "PROCESS_REVIEW_BANNER_DFP",
  "PrefetchCount": 1
},
"Selenium": {
  "Headless": true,
  "PageLoadTimeoutSeconds": 75,
  "UserDataDirs": "D:\\Login\\1",
  "PageLoadStrategy": "eager"
}
```

- **`UserDataDirs`**: profile Chrome đã đăng nhập GAM (có thể nhiều path, cách nhau bởi dấu phẩy).
- **`QueueName`**: đổi cho đúng queue team vận hành (khác queue `PROCESS_SETUP_BANNER_DFP` của app submit).

Ghi đè bằng biến môi trường (ưu tiên cao hơn file):

```powershell
$env:RabbitMq__QueueName = "PROCESS_REVIEW_BANNER_DFP"
$env:Runtime__WorkerMode = "Blocked"
$env:Selenium__UserDataDirs = "D:\Login\1,D:\Login\2"
```

| Exit code | Ý nghĩa |
|-----------|---------|
| 0 | Thành công |
| 1 | Lỗi (thiếu key, không tìm thấy file, lỗi API) |
| 2 | Model trả về text không parse được |

---

## Cấu hình (`appsettings.json`)

### Gemini

```json
"Gemini": {
  "Model": "gemini-2.5-flash",
  "ApiKey": "key-project-1,key-project-2,key-project-3"
}
```

- Nhiều key: **phân tách bằng dấu phẩy** (có thể có khoảng trắng sau dấu phẩy).
- Mỗi lần gọi API: **chọn ngẫu nhiên** một key trong danh sách.
- Biến môi trường `GEMINI_API_KEY` (cũng hỗ trợ nhiều key cách nhau bởi dấu phẩy) ghi đè appsettings.

### Tiêu chí review banner

```json
"BannerReview": {
  "BlockedLabel": "Blocked",
  "ReviewedLabel": "Reviewed",
  "BlockedCategories": [
    {
      "Name": "Gambling",
      "Description": "Gambling, betting, casino, lottery.",
      "Keywords": [ "gambling", "betting", "casino", "lottery" ]
    }
  ]
}
```

| Trường | Mô tả |
|--------|--------|
| `BlockedLabel` / `ReviewedLabel` | Nhãn in ra console (và dùng trong prompt) |
| `Name` | Mã danh mục (dùng khi mở rộng API) |
| `Description` | Mô tả gửi cho model trong prompt |
| `Keywords` | Gợi ý từ khóa cho model (có thể bổ sung từ API sau) |

Banner **không** khớp bất kỳ danh mục block nào → **`Reviewed`**.

### Telegram (thông báo)

```json
"Telegram": {
  "Enabled": true,
  "BotToken": "your-bot-token",
  "ChatId": "your-chat-id"
}
```

| Sự kiện | Thông báo Telegram |
|---------|-------------------|
| Exception ở bất kỳ bước | Tin nhắn ⚠️ Exception + context |
| API key Gemini thiếu / hết quota / hết hạn | Tin nhắn 🔑 API key / quota |
| LLM không trả `Blocked`/`Reviewed` | Tin nhắn 🤖 + raw response |
| Review thành công | **Gửi ảnh** kèm caption kết quả `Blocked` hoặc `Reviewed` |

**Lấy Chat ID:** nhắn tin cho bot trên Telegram, mở:

`https://api.telegram.org/bot<BotToken>/getUpdates`

→ tìm `"chat":{"id": ...}` và điền vào `ChatId`.

Biến môi trường (ghi đè appsettings): `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`.

---

## Kiến trúc (SOLID)

```
┌─────────────────────────────────────────────────────────────┐
│  Program.cs          Composition Root — chỉ DI + CLI        │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│  Application         ReviewBannerUseCase (orchestration)     │
│                      + Abstractions (interfaces)              │
└────────────┬───────────────────────────────┬────────────────┘
             │                               │
┌────────────▼────────────┐    ┌─────────────▼────────────────┐
│  Domain                 │    │  Infrastructure               │
│  Models, VerdictParser  │    │  Gemini, Files, appsettings   │
│  (không phụ thuộc SDK)  │    │  (Google.GenAI, I/O)          │
└─────────────────────────┘    └───────────────────────────────┘
```

### Các layer

| Layer | Thư mục | Trách nhiệm |
|-------|---------|-------------|
| **Domain** | `Domain/` | Khái niệm nghiệp vụ: policy, ảnh, verdict; parse `Blocked`/`Reviewed` |
| **Application** | `Application/` | Use case: điều phối đọc ảnh → policy → prompt → Gemini → parse |
| **Infrastructure** | `Infrastructure/` | Triển khai cụ thể: file, Gemini SDK, đọc `appsettings` |
| **Configuration** | `Configuration/` | DTO bind JSON (không chứa logic) |
| **DependencyInjection** | `DependencyInjection/` | Đăng ký service |

### Luồng xử lý một ảnh

1. `LocalImageReader` — đọc bytes + MIME type.
2. `AppSettingsPolicyProvider` — load `ReviewPolicy` từ config.
3. `BannerReviewPromptBuilder` — tạo prompt ngắn từ policy.
4. `GeminiVisionAnalyzer` — gọi `generateContent` (ảnh inline + prompt).
5. `VerdictParser` — chuẩn hóa response → `Blocked` / `Reviewed`.

---

## Mở rộng trong tương lai

| Nhu cầu | Cách làm (không sửa Use Case) |
|---------|-------------------------------|
| Policy từ API | Thêm class `ApiReviewPolicyProvider : IReviewPolicyProvider`, đổi registration trong `ServiceCollectionExtensions` |
| Nhiều API key | Decorator bọc `IBannerVisionAnalyzer`, đổi key khi 429 |
| Web API | ASP.NET Core inject `ReviewBannerUseCase` vào controller |
| Model khác | Class mới implement `IBannerVisionAnalyzer` |

---

## Biến môi trường

| Biến | Bắt buộc | Mô tả |
|------|----------|--------|
| `DOTNET_ENVIRONMENT` | Production | `Production` hoặc `ProductionBlocked` |
| `Gemini:ApiKey` (appsettings) | Mặc định | API key Gemini |
| `GEMINI_API_KEY` | Tùy chọn | Ghi đè appsettings khi deploy |
| `GOOGLE_API_KEY` | Tùy chọn | Ghi đè thay `GEMINI_API_KEY` |
| `TELEGRAM_BOT_TOKEN` | Thay thế | Ghi đè `Telegram:BotToken` |
| `TELEGRAM_CHAT_ID` | Có (Telegram) | ID chat nhận thông báo |
| `Runtime__Environment` | Tùy chọn | `Test` / `Production` |
| `Runtime__WorkerMode` | Tùy chọn | `Reviewed` / `Blocked` |
| `RabbitMq__HostName` | Production | Host RabbitMQ |
| `RabbitMq__QueueName` | Production | Tên queue |
| `Selenium__UserDataDirs` | Production | Profile Chrome GAM |

\* Một trong hai biến Gemini phải có giá trị. Telegram cần `ChatId` để gửi tin.

---

## Ghi chú vận hành

- **Free tier:** quota theo project (~250 request/ngày với `gemini-2.5-flash`, xem AI Studio).
- **Model:** `gemini-2.0-flash` có thể hết quota trên một số project; ưu tiên `gemini-2.5-flash`.
- **Bảo mật:** không commit API key; dùng biến môi trường hoặc secret manager.

---

## Cấu trúc thư mục mã nguồn

```
AdOpsAgenReviewBanner/
├── Program.cs
├── appsettings.json
├── appsettings.Production.json
├── appsettings.Production.Blocked.json
├── HUONG_DAN.md
├── Configuration/          # DTO appsettings
├── Domain/                 # Nghiệp vụ thuần
├── Application/            # Use case + interfaces
├── Infrastructure/         # Gemini, file, prompt
└── DependencyInjection/    # AddBannerReview()
```

---

## Tham khảo

- [Gemini — Hiểu hình ảnh](https://ai.google.dev/gemini-api/docs/image-understanding)
- [Gemini — Rate limits](https://ai.google.dev/gemini-api/docs/rate-limits)

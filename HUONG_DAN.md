# AdOps Agent Review Banner — Hướng dẫn

Ứng dụng console .NET 8 dùng **Florence-2 ONNX (local, không tốn phí API)** để review banner quảng cáo (hình ảnh) và trả về một trong hai kết quả: **`Blocked`** hoặc **`Reviewed`**.

> **Gemini API** đã được **tạm tắt** (có phí). Code Gemini vẫn giữ trong `Infrastructure/Gemini/` (bọc `#if false`) để bật lại khi cần.

---

## Mục tiêu

- Tự động phân loại banner theo tiêu chí Ad Ops (cờ bạc, nội dung người lớn, lừa đảo, v.v.).
- Kiến trúc **SOLID**, dễ mở rộng: đổi nguồn policy (API), đổi model vision, thêm API HTTP sau này.
- Cấu hình tiêu chí block qua **`appsettings.json`** (sau này có thể thay bằng API).

---

## Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download) — chạy **x64**
- Dung lượng ~1 GB cho mô hình Florence-2 (tải tự động lần đầu)
- (Tùy chọn) `tessdata/eng.traineddata` cho Tesseract OCR bổ sung
- (Tùy chọn) Telegram Bot Token + Chat ID để nhận ảnh kết quả

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

Breakpoint gợi ý: `ReviewBannerUseCase.ExecuteAsync` (bước gọi Florence-2) hoặc `FlorenceBannerModerationScanner.ScanAsync`.

---

## Cài đặt và chạy

```powershell
cd F:\PROJECT\OCR_DETECT_BANNER_GAM\AdOpsAgenReviewBanner

# Lần đầu chạy: Florence-2 tự tải ONNX vào thư mục Models/ (có thể mất vài phút)

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

## Deploy lên Server — File cấu hình & tham số

Phần này mô tả **chính xác** những gì cần chuẩn bị khi đưa app lên server Production (không dùng chế độ TEST local).

### Kiến trúc trên server (khuyến nghị: 2 process / 2 thư mục)

```
                    ┌─────────────────────────────────────┐
                    │         RabbitMQ (cùng 1 queue)        │
                    └──────────────┬──────────────────────┘
                                   │
          mode=reviewed            │            mode=execute_plan
                    ┌──────────────▼──────────────┐
                    │  Instance 1 — Worker Reviewed │
                    │  DOTNET_ENVIRONMENT=Production│
                    │  Florence → Mongo → publish EP  │
                    └──────────────┬──────────────┘
                                   │ auto publish
                    ┌──────────────▼──────────────────┐
                    │ Instance 2 — Worker ExecutePlan │
                    │ appsettings.json                │
                    │   WorkerMode=ExecutePlan        │
                    │ GAM Allow/Block → is_review=1   │
                    └─────────────────────────────────┘
```

**Clone 2 thư mục deploy** trên server — mỗi thư mục một `appsettings.json` khác nhau (`WorkerMode`: Reviewed vs ExecutePlan), click `.exe` hoặc Task Scheduler.

---

### Danh sách file bắt buộc trên server

| # | File / thư mục | Reviewed (instance 1) | ExecutePlan (instance 2) | Ghi chú |
|---|----------------|----------------------|--------------------------|---------|
| 1 | `AdOpsAgenReviewBanner.dll` + dependency | Có | Có | Sau `dotnet publish` |
| 2 | `appsettings.json` | Có (`WorkerMode=Reviewed`) | Có (`WorkerMode=ExecutePlan`) | **Một file duy nhất / clone** — copy từ `appsettings.example.json` + template worker |
| 3 | `Models/` (Florence ONNX ~1 GB) | **Có** | **Không** | ExecutePlan **không đăng ký / không tải** Florence |
| 4 | `tessdata/` (vie + eng) | Khuyến nghị | **Không** | Chỉ worker Reviewed dùng OCR |
| 5 | Chrome profile (`Selenium:UserDataDirs`) | Có | Có | Đã login GAM — **profile riêng** mỗi worker |
| 6 | Chrome + ChromeDriver | Có | Có | Khớp version |
| 7 | `.env` | Tùy chọn | Tùy chọn | Ghi đè secret — không commit Git |

> **Lưu ý Git:** `appsettings.json` nằm trong `.gitignore`. Sao chép từ `appsettings.example.json` + `appsettings.Production.example.json` hoặc `appsettings.ExecutePlan.example.json`.
>
> **Lưu ý code:** `Runtime:WorkerMode=ExecutePlan` → DI **bỏ qua** toàn bộ Florence stack (`FlorenceBannerModerationScanner`, `ReviewBannerUseCase`, …). Log khởi động: `RabbitMQ consumer started ... (không tải Florence)`.

---

### Thứ tự load cấu hình (ASP.NET Configuration)

| Nguồn config | File được load (sau cùng ghi đè trước) |
|--------------|-------------------------------------------|
| Click `.exe` / `DOTNET_ENVIRONMENT=Production` | Chỉ `appsettings.json` (code bỏ qua `appsettings.Production.json` nếu còn file cũ) |

**Chọn Reviewed hay ExecutePlan:** sửa `Runtime:WorkerMode` trong `appsettings.json` của từng clone (template: `appsettings.Production.example.json` / `appsettings.ExecutePlan.example.json`).

Biến môi trường dạng `Section__Key` **ghi đè** mọi file JSON (ưu tiên cao nhất — dùng khi debug local với launch profile).

---

### Chi tiết từng file cấu hình

#### 1. `appsettings.json` — cấu hình nền (dùng chung cả 2 worker)

Đặt trên server cùng thư mục với `.dll`. Các section quan trọng:

| Section | Tham số | Giá trị gợi ý Production | Mô tả |
|---------|---------|--------------------------|--------|
| **Telegram** | `Enabled` | `true` | Bật gửi ảnh + cảnh báo lỗi |
| | `BotToken` | token bot thật | Hoặc `TELEGRAM_BOT_TOKEN` |
| | `ChatId` | ID chat nhóm Ad Ops | Hoặc `TELEGRAM_CHAT_ID` |
| **Florence** | `ModelsPath` | `Models` | Thư mục ONNX (tương đối hoặc absolute path) |
| | `TessDataPath` | `tessdata` | Thư mục Tesseract |
| | `EnableTesseract` | `true` | OCR bổ sung |
| | `TesseractLanguages` | `vie+eng` | Ngôn ngữ OCR |
| **Mongo** | `Enabled` | `true` | **Bắt buộc** trên Production |
| | `ConnectionString` | `mongodb://user:pass@host:27017` | Server Mongo thật |
| | `Database` | `news_fptonline` | DB catalog |
| | `CollectionBannerReview` | `BannerDfpScreenShot` | Collection lưu kết quả review |
| **GamReview** | `NetworkCode` | `27973503` | Mã network GAM |
| | `BlockedReviewCenterUrl` | URL Ad review center | ExecutePlan mở trang này để filter Creative ID |
| | `GridWaitSeconds` | `45` | Chờ lưới GAM (Reviewed) |
| | `PreviewInitialDelaySeconds` | `3` | Delay trước chụp preview |
| | `EnableGridPagination` | `true` | Lật trang lưới GAM |
| **BannerReview** | `BlockedCategories` | `[...]` | Từ khóa block bổ sung (tùy chọn) |
| **Runtime** | `Environment` | Để `Test` trong file nền cũng được | **Production.json sẽ ghi đè thành `Production`** |

```json
"Mongo": {
  "Enabled": true,
  "ConnectionString": "mongodb://10.1.11.148:27017",
  "Database": "news_fptonline",
  "CollectionBannerReview": "BannerDfpScreenShot"
}
```

---

#### 2. `appsettings.Production.json` — worker **Reviewed** (instance 1)

Tạo từ `appsettings.Production.example.json`. Chỉ override những gì khác `appsettings.json`:

| Section | Tham số | Giá trị mẫu (tham chiếu project) | Bắt buộc |
|---------|---------|-----------------------------------|----------|
| **Runtime** | `Environment` | `Production` | Có — bật consumer RabbitMQ |
| | `WorkerMode` | `Reviewed` | Có |
| **RabbitMq** | `HostName` | `103.163.216.115` | Có |
| | `VirtualHost` | `/` | Có — hỏi team infra nếu khác |
| | `UserName` | `web_push` | Có |
| | `Password` | `***` | Có — không commit Git |
| | `Port` | `5672` | Có |
| | `QueueName` | `PROCESS_REVIEW_BANNER_DFP` | Có — queue consume Reviewed |
| | `ExecutePlanQueueName` | `PROCESS_EXECUTE_PLAN_DFP` | Có — queue publish/consume ExecutePlan |
| | `PrefetchCount` | `1` | Khuyến nghị 1 (xử lý tuần tự) |
| | `PublishExecutePlanAfterMongoInsert` | `true` | Có — tự đẩy message cho worker 2 |
| **GamReview** | `BotIndex` | `bot_review\|1` | Có — ghi Mongo, phân biệt bot |
| **Selenium** | `Headless` | `true` | Có trên server |
| | `PageLoadTimeoutSeconds` | `75` | Khuyến nghị cao hơn local |
| | `UserDataDirs` | `D:\Login\review_profile` | Có — profile Chrome đã login GAM |
| | `PageLoadStrategy` | `eager` | Tùy chọn |

**Chạy instance 1:**

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
dotnet AdOpsAgenReviewBanner.dll
```

---

#### 3. `appsettings.Production.json` — worker **ExecutePlan** (instance 2)

Copy từ `appsettings.Production.ExecutePlan.example.json` → đặt tên `appsettings.Production.json` trong thư mục clone ExecutePlan.

**Không cần** copy `Models/`, `tessdata/` — chỉ Selenium GAM (Allow/Block).

| Section | Tham số | Khác Reviewed |
|---------|---------|---------------|
| **Runtime** | `WorkerMode` | `ExecutePlan` |
| **GamReview** | `BotIndex` | `bot_execute_plan\|1` |
| **RabbitMq** | `PublishExecutePlanAfterMongoInsert` | `false` |
| **Selenium** | `UserDataDirs` | profile Chrome **riêng** |

**Chạy instance 2:** double-click `AdOpsAgenReviewBanner.exe` trong folder clone (hoặc `dotnet AdOpsAgenReviewBanner.dll`).

---

#### 4. Biến môi trường (secret / debug local — ghi đè JSON)

| Biến | Instance | Ví dụ | Mô tả |
|------|----------|-------|--------|
| `DOTNET_ENVIRONMENT` | Cả 2 | `Production` | Mặc định khi publish / click exe |
| `Runtime__WorkerMode` | Tùy chọn | `Reviewed` / `ExecutePlan` | Chỉ khi cần ghi đè tạm — mặc định sửa `appsettings.json` |
| `RabbitMq__HostName` | Cả 2 | `103.163.216.115` | Ghi đè host |
| `RabbitMq__Password` | Cả 2 | `***` | Nên set qua env, không ghi file |
| `RabbitMq__QueueName` | Reviewed | `PROCESS_REVIEW_BANNER_DFP` | Queue consume Reviewed |
| `RabbitMq__ExecutePlanQueueName` | ExecutePlan | `PROCESS_EXECUTE_PLAN_DFP` | Queue consume ExecutePlan |
| `Mongo__ConnectionString` | Cả 2 | `mongodb://...` | Connection Mongo server |
| `Selenium__UserDataDirs` | Từng instance | path profile riêng | **Khác nhau** giữa 2 worker |
| `TELEGRAM_BOT_TOKEN` | Cả 2 | token | Ghi đè Telegram |
| `TELEGRAM_CHAT_ID` | Cả 2 | chat id | Ghi đè Telegram |

Tham khảo đầy đủ: `.env.example` trong project.

---

### Message queue — tham số publisher cần biết

**Vào queue (từ hệ thống khác → Reviewed):**

```json
{
  "link_review": "https://admanager.google.com/27973503#creatives/ad_review_center/...",
  "mode": "reviewed",
  "order": 5,
  "_category": "Business & Industrial"
}
```

| Trường | Ý nghĩa |
|--------|---------|
| `order` **(bắt buộc**, số nguyên ≥ 1) | Thứ tự category trong filter **General ad category** trên GAM (1-based). Ví dụ `5` = **Business & Industrial**. |
| `_category` *(tùy chọn)* | Tên category từ n8n/DB. Worker lưu vào Mongo field `category_name` khi insert banner. |
| *(tự động)* | `user_name` luôn gán `"n8n"` khi insert từ luồng Reviewed. |

**Tự sinh ra sau Mongo (Reviewed → ExecutePlan):**

```json
{
  "creative_id": "AJILAY...",
  "action": "Blocked",
  "mode": "execute_plan"
}
```

| Trường `action` | Ý nghĩa trên GAM |
|-----------------|------------------|
| `Blocked` / `block` | Block creative |
| `Reviewed` / `allow` | Allow (duyệt) creative |

---

### Checklist deploy server

**Chung (cả 2 instance):**

- [ ] Cài **.NET 8 Runtime x64**
- [ ] Cấu hình **Mongo** `Enabled=true` + connection string server thật
- [ ] Cấu hình **RabbitMQ** — 2 queue durable: `PROCESS_REVIEW_BANNER_DFP` + `PROCESS_EXECUTE_PLAN_DFP`
- [ ] Chuẩn bị **2 Chrome profile** đã login GAM (`Selenium:UserDataDirs` khác nhau)

**Instance 1 — Reviewed:**

- [ ] `dotnet publish -c Release -o D:\Deploy\AdOpsReview-Reviewed`
- [ ] `appsettings.Production.json` từ `appsettings.Production.example.json` (`WorkerMode=Reviewed`)
- [ ] Copy hoặc để tự tải `Models/` Florence (~1 GB)
- [ ] Copy `tessdata/` (vie + eng) nếu bật Tesseract
- [ ] Telegram nhận ảnh review test
- [ ] Gửi message `mode=reviewed` → Mongo insert + publish `execute_plan`

**Instance 2 — ExecutePlan:**

- [ ] `dotnet publish -c Release -o D:\Deploy\AdOpsReview-ExecutePlan` (thư mục riêng)
- [ ] `appsettings.Production.json` từ `appsettings.Production.ExecutePlan.example.json` (`WorkerMode=ExecutePlan`)
- [ ] **Không** cần `Models/`, **không** cần `tessdata/`
- [ ] Log khởi động có `(không tải Florence)`
- [ ] Nhận message `mode=execute_plan` → GAM Allow/Block → `is_review=1`

---

### Publish & chạy nền (Windows Server)

```powershell
# Build release
cd F:\PROJECT\AdOpsAgenReviewBanner
dotnet publish -c Release -o D:\Deploy\AdOpsReview-Reviewed

# Instance Reviewed — sửa appsettings.Production.json (WorkerMode=Reviewed) rồi chạy exe
cd D:\Deploy\AdOpsReview-Reviewed
.\AdOpsAgenReviewBanner.exe

# Instance ExecutePlan — clone riêng, Production.json từ ExecutePlan.example.json
dotnet publish -c Release -o D:\Deploy\AdOpsReview-ExecutePlan
cd D:\Deploy\AdOpsReview-ExecutePlan
.\AdOpsAgenReviewBanner.exe
```

Log khởi động thành công:

```
# Reviewed
RabbitMQ consumer started. Worker mode=Reviewed, environment=Production (Florence khi xử lý banner)
 [*] Waiting for messages on queue=PROCESS_REVIEW_BANNER_DFP, workerMode=Reviewed

# ExecutePlan
RabbitMQ consumer started. Worker mode=ExecutePlan, environment=Production (không tải Florence)
 [*] Waiting for messages on queue=PROCESS_EXECUTE_PLAN_DFP, workerMode=ExecutePlan
```

---

## Môi trường TEST / PRODUCTION

| File | Mục đích |
|------|----------|
| `appsettings.json` | Local: `Runtime.Environment = Test`, quét folder / file / URL |
| `appsettings.Production.json` | **Một file / clone** — `WorkerMode` Reviewed hoặc ExecutePlan |
| `appsettings.Production.example.json` | Template clone **Reviewed** |
| `appsettings.Production.ExecutePlan.example.json` | Template clone **ExecutePlan** |

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

> Chi tiết file cấu hình & checklist deploy: xem mục **[Deploy lên Server](#deploy-lên-server--file-cấu-hình--tham-số)** phía trên.

1. Tạo queue trên RabbitMQ (ví dụ `PROCESS_REVIEW_BANNER_DFP`, durable).
2. Publisher gửi JSON:

```json
{
  "link_review": "https://admanager.google.com/...",
  "mode": "reviewed",
  "order": 5
}
```

Message **execute_plan** (thực thi kết quả review trên GAM):

```json
{
  "creative_id": "AJILAY...",
  "action": "Blocked",
  "mode": "execute_plan"
}
```

`mode`: `reviewed` hoặc `execute_plan` — worker chỉ xử lý message **trùng** `Runtime.WorkerMode`.

**Luồng nối Reviewed → ExecutePlan (Production):**

1. Worker **Reviewed** nhận message `mode=reviewed` + `order` → filter General ad category → **quét `iframe[id]` trên listing** (so Mongo `iframe_id`) → mở banner đầu tiên chưa có → Florence → lưu Mongo (`is_review=0`). Cả trang đã có trong Mongo → **Next page** không mở preview.
2. Sau insert Mongo thành công, tự động **publish** message `mode=execute_plan` vào **cùng queue**:
   - `is_block_ads=true` → `"action": "Blocked"`
   - `is_block_ads=false` → `"action": "Reviewed"` (Allow trên GAM)
3. Worker **ExecutePlan** nhận message → apply Allow/Block trên GAM → `is_review=1`.

Bật/tắt publish: `RabbitMq:PublishExecutePlanAfterMongoInsert` (`true` trong `appsettings.Production.json`).

3. Chạy worker **Reviewed**:

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run
# hoặc sau publish:
# dotnet AdOpsAgenReviewBanner.dll
```

4. Chạy worker **ExecutePlan** (process riêng — thực thi kết quả review):

```powershell
# Local debug: launch profile "PRODUCTION - RabbitMQ ExecutePlan"
# Server: copy appsettings.Production.ExecutePlan.example.json → appsettings.Production.json rồi click exe
dotnet run --launch-profile "PRODUCTION - RabbitMQ ExecutePlan"
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
  "HostName": "103.163.216.115",
  "VirtualHost": "/",
  "UserName": "web_push",
  "Password": "your-rabbitmq-password",
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
$env:Runtime__WorkerMode = "ExecutePlan"
$env:Selenium__UserDataDirs = "D:\Login\1,D:\Login\2"
```

| Exit code | Ý nghĩa |
|-----------|---------|
| 0 | Thành công |
| 1 | Lỗi (thiếu key, không tìm thấy file, lỗi API) |
| 2 | Model trả về text không parse được |

---

## Cấu hình (`appsettings.json`)

### Florence-2 (vision local — đang dùng)

```json
"Florence": {
  "ModelsPath": "Models",
  "TessDataPath": "tessdata",
  "EnableTesseract": true
}
```

| Trường | Mô tả |
|--------|--------|
| `ModelsPath` | Thư mục chứa ONNX; lần đầu `FlorenceModelDownloader` tự tải (~1 GB) |
| `TessDataPath` | Thư mục Tesseract (`eng.traineddata`); thiếu thì chỉ dùng Florence OCR |
| `EnableTesseract` | Bật/tắt OCR Tesseract bổ sung |

**Logic phân loại** (clone từ `CONVERT_IMG_TO_TEXT`):

1. Florence-2: `MORE_DETAILED_CAPTION` + `OCR`
2. Tesseract OCR (tùy chọn)
3. So khớp từ khóa block/review + rule ngữ cảnh cờ bạc (`game` + `money/bonus/casino`…)
4. Map kết quả → nhãn GAM:
   - `BLOCKED` hoặc `REVIEW` → **`Blocked`**
   - `ALLOW` → **`Reviewed`**

### Gemini (tạm tắt)

```json
"Gemini": {
  "Model": "gemini-2.5-flash",
  "ApiKey": "key-project-1,key-project-2"
}
```

Bật lại: gỡ `#if false` trong `Infrastructure/Gemini/`, uncomment registration trong `ServiceCollectionExtensions.cs`.

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
| `Description` | Mô tả danh mục (dùng khi bật lại Gemini prompt) |
| `Keywords` | Từ khóa **bổ sung** cho Florence keyword matcher (ngoài danh sách mặc định) |

Banner **không** khớp từ khóa block và Florence mô tả được ảnh → **`Reviewed`**.

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
| API key Gemini thiếu / hết quota (khi bật Gemini) | Tin nhắn 🔑 API key / quota |
| Analyzer không trả `Blocked`/`Reviewed` | Tin nhắn 🤖 + raw response |
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
│  Models, VerdictParser  │    │  Florence, Files, Telegram    │
│  (không phụ thuộc SDK)  │    │  (ONNX, I/O)                  │
└─────────────────────────┘    └───────────────────────────────┘
```

### Các layer

| Layer | Thư mục | Trách nhiệm |
|-------|---------|-------------|
| **Domain** | `Domain/` | Khái niệm nghiệp vụ: policy, ảnh, verdict; parse `Blocked`/`Reviewed` |
| **Application** | `Application/` | Use case: điều phối đọc ảnh → policy → Florence-2 → parse |
| **Infrastructure** | `Infrastructure/` | Triển khai: Florence-2, Tesseract, file, Telegram, Mongo, Selenium |
| **Configuration** | `Configuration/` | DTO bind JSON (không chứa logic) |
| **DependencyInjection** | `DependencyInjection/` | Đăng ký service |

### Luồng xử lý một ảnh

1. `LocalImageReader` — đọc bytes + MIME type.
2. `AppSettingsPolicyProvider` — load `ReviewPolicy` từ config.
3. `BannerReviewPromptBuilder` — tạo prompt (giữ cho tương thích interface; Florence không dùng prompt).
4. `FlorenceBannerVisionAnalyzer` → `FlorenceBannerModerationScanner`:
   - Caption + OCR (Florence-2)
   - OCR bổ sung (Tesseract, tùy chọn)
   - `BannerKeywordMatcher` — rule từ khóa + `BlockedCategories` từ config
5. `VerdictParser` — chuẩn hóa nhãn → `Blocked` / `Reviewed`.
6. `TelegramNotifier.NotifyReviewResultAsync` — **gửi ảnh** + caption kết quả.

---

## Mở rộng trong tương lai

| Nhu cầu | Cách làm (không sửa Use Case) |
|---------|-------------------------------|
| Policy từ API | Thêm class `ApiReviewPolicyProvider : IReviewPolicyProvider`, đổi registration trong `ServiceCollectionExtensions` |
| Nhiều API key | Decorator bọc `IBannerVisionAnalyzer`, đổi key khi 429 |
| Web API | ASP.NET Core inject `ReviewBannerUseCase` vào controller |
| Bật lại Gemini | Uncomment DI + gỡ `#if false` trong `Infrastructure/Gemini/` |
| Model khác | Class mới implement `IBannerVisionAnalyzer` |

---

## Biến môi trường

| Biến | Bắt buộc | Mô tả |
|------|----------|--------|
| `DOTNET_ENVIRONMENT` | Production | Luôn `Production` (click exe / publish) |
| `Florence:ModelsPath` | Tùy chọn | Thư mục ONNX Florence-2 |
| `Gemini:ApiKey` | Chỉ khi bật Gemini | API key Gemini |
| `GEMINI_API_KEY` | Chỉ khi bật Gemini | Ghi đè appsettings |
| `TELEGRAM_BOT_TOKEN` | Thay thế | Ghi đè `Telegram:BotToken` |
| `TELEGRAM_CHAT_ID` | Có (Telegram) | ID chat nhận thông báo |
| `Runtime__Environment` | Tùy chọn | `Test` / `Production` |
| `Runtime__WorkerMode` | Tùy chọn | `Reviewed` / `ExecutePlan` |
| `RabbitMq__HostName` | Production | Host RabbitMQ |
| `RabbitMq__QueueName` | Reviewed | Queue consume Reviewed |
| `RabbitMq__ExecutePlanQueueName` | ExecutePlan | Queue consume ExecutePlan |
| `Selenium__UserDataDirs` | Production | Profile Chrome GAM |

\* Florence-2 không cần API key. Telegram cần `ChatId` để gửi tin/ảnh.

---

## Ghi chú vận hành

- **Florence-2:** chỉ worker **Reviewed** — ONNX local (x64), lần đầu tải ~1 GB.
- **ExecutePlan:** không tải Florence; chỉ cần Chrome + RabbitMQ + Mongo.
- **RAM:** Reviewed khuyến nghị ≥ 8 GB; ExecutePlan nhẹ hơn (chủ yếu Selenium).
- **Tesseract:** copy `eng.traineddata` vào `tessdata/` (có thể lấy từ project `CONVERT_IMG_TO_TEXT`).
- **Unit test Telegram:** `dotnet test` — kiểm tra `sendPhoto` khi file ảnh tồn tại.

---

## Cấu trúc thư mục mã nguồn

```
AdOpsAgenReviewBanner/
├── Program.cs
├── appsettings.json
├── appsettings.Production.json
├── appsettings.Production.ExecutePlan.example.json
├── HUONG_DAN.md
├── Configuration/          # DTO appsettings
├── Domain/                 # Nghiệp vụ thuần
├── Application/            # Use case + interfaces
├── Infrastructure/
│   ├── Florence/           # Florence-2, keyword matcher, Tesseract
│   ├── Gemini/             # (tạm tắt #if false)
│   ├── Telegram/
│   └── ...
├── Tests/                  # Unit test (Telegram sendPhoto, UseCase flow)
└── DependencyInjection/    # AddBannerReview()
```

### Chạy unit test

```powershell
cd F:\PROJECT\AdOpsAgenReviewBanner
dotnet test
```

Test quan trọng:

| File | Kiểm tra |
|------|----------|
| `Tests/TelegramNotifierTests.cs` | HTTP gọi `sendPhoto` khi ảnh tồn tại |
| `Tests/ReviewBannerUseCaseTelegramTests.cs` | Use case gọi `NotifyReviewResultAsync` với đúng đường dẫn ảnh |

---

## Tham khảo

- [Florence-2 (NuGet)](https://www.nuget.org/packages/Florence2)
- Project mẫu keyword/OCR: `F:\PROJECT\CAPTCHA_TO_TEXT\CONVERT_IMG_TO_TEXT`
- [Gemini — Hiểu hình ảnh](https://ai.google.dev/gemini-api/docs/image-understanding) (khi bật lại Gemini)

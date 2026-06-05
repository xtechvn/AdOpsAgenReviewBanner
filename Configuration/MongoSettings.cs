namespace AdOpsAgenReviewBanner.Configuration;

/// <summary>
/// Cấu hình MongoDB — map từ CONSUMMER_SIS_REPORT (App.config: ConnectionMongo, MongoCatalog, ...).
/// Kết nối: new MongoClient(ConnectionString) → GetDatabase(Database) → GetCollection(...).
/// </summary>
public sealed class MongoSettings
{
    /// <summary>Connection string MongoDB. VD: mongodb://localhost:27017 hoặc có auth + authSource=admin.</summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>Tên database (CONSUMMER_SIS_REPORT: MongoCatalog).</summary>
    public string Database { get; set; } = "news_fptonline";

    /// <summary>Collection lưu log kết quả review banner (link, mode, verdict, thời gian).</summary>
    public string CollectionBannerReview { get; set; } = "BannerDfpScreenShot";

    /// <summary>Bật ghi Mongo sau mỗi lần review. false = chỉ dùng Telegram/console.</summary>
    public bool Enabled { get; set; } = false;
}

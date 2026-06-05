using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AdOpsAgenReviewBanner.Domain.Models;

/// <summary>
/// Document MongoDB lưu thông tin banner DFP cần review (bot chụp ảnh / phân loại).
/// Tên field giữ snake_case để khớp collection hiện có trên Mongo.
/// </summary>
public sealed class BannerReviewDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? _id { get; set; }

    /// <summary>URL ảnh banner (take_screen_dfp_ads).</summary>
    public string link { get; set; } = "";

    /// <summary>Trạng thái xử lý bot (VD: 2 = đã chụp / chờ review).</summary>
    public int status { get; set; }

    public bool is_block_ads { get; set; }

    public int cause_id { get; set; }

    public string cause { get; set; } = "";

    /// <summary>Unix timestamp (giây) — thời điểm chụp ảnh.</summary>
    [BsonRepresentation(BsonType.Int64)]
    public long take_time { get; set; }

    /// <summary>Unix timestamp (giây) — thời điểm GAM ghi nhận.</summary>
    [BsonRepresentation(BsonType.Int64)]
    public long dfp_time { get; set; }

    public string iframe_id { get; set; } = "";

    public string link_iframe { get; set; } = "";

    public string creative_id { get; set; } = "";

    /// <summary>URL landing page của quảng cáo.</summary>
    public string url { get; set; } = "";

    /// <summary>Unix timestamp (giây) — thời điểm bot xử lý.</summary>
    [BsonRepresentation(BsonType.Int64)]
    public long process_time { get; set; }

    /// <summary>VD: bot_142_01|1</summary>
    public string bot_index { get; set; } = "";

    /// <summary>0 = chưa review; 1 = đã review.</summary>
    public int is_review { get; set; }

    /// <summary>Unix timestamp (giây) — thời điểm review; 0 nếu chưa review.</summary>
    [BsonRepresentation(BsonType.Int64)]
    public long review_time { get; set; }

    public string? note { get; set; }

    public int category_id { get; set; }

    public string? category_name { get; set; }

    public int user_id { get; set; }

    public string? user_name { get; set; }

    /// <summary>Chuỗi JSON — parse bằng <see cref="BannerDetectInfo"/> khi cần.</summary>
    public string? detect_info { get; set; }

    public double total_time { get; set; }
}

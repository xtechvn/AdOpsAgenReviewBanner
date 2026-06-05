using System.Text.Json;
using AdOpsAgenReviewBanner.Domain.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class BannerReviewDocumentTests
{
    [Fact]
    public void Deserialize_MongoExtendedJson_MapsAllFields()
    {
        const string extendedJson = """
            {
              "_id": { "$oid": "65904c9521704a81da598be3" },
              "link": "http://st-cs.fptonline.net/take_screen_dfp_ads/image/2023/12/31/ID__darc-ad-preview-div-81_preview_c675855926845_v0_300_600__1703289601.31476.png",
              "status": 2,
              "is_block_ads": false,
              "cause_id": 0,
              "cause": "",
              "take_time": { "$numberLong": "1703980801" },
              "dfp_time": { "$numberLong": "1703980805" },
              "iframe_id": "c675855926845_v0_300_600_",
              "link_iframe": "https://tpc.googlesyndication.com/safeframe/1-0-40/html/container.html",
              "creative_id": "AILLT9iIXp/wmYJKpxKiGfnPp9KjObR/Rp+uVtDbVtydsLOSqJHEcNU=",
              "url": "https://mattresswarehouse.com/",
              "process_time": { "$numberLong": "1703980934" },
              "bot_index": "bot_142_01|1",
              "is_review": 0,
              "review_time": { "$numberLong": "0" },
              "note": null,
              "category_id": 0,
              "category_name": null,
              "user_id": 0,
              "user_name": null,
              "detect_info": "{\"text\":\"ENJOY OUR Year Price Guarantee\",\"text_vietnamese\":\"\",\"time_detect_image\":0.24996,\"time_detect_text\":2.68955,\"time_reg_eng\":0.28369,\"time_reg_vn\":0.0}",
              "total_time": 3.35917
            }
            """;

        var doc = BsonSerializer.Deserialize<BannerReviewDocument>(BsonDocument.Parse(extendedJson));

        Assert.Equal("65904c9521704a81da598be3", doc._id);
        Assert.Contains("take_screen_dfp_ads", doc.link);
        Assert.Equal(2, doc.status);
        Assert.False(doc.is_block_ads);
        Assert.Equal(1703980801, doc.take_time);
        Assert.Equal("bot_142_01|1", doc.bot_index);
        Assert.Equal(0, doc.is_review);
        Assert.Equal(3.35917, doc.total_time);

        var detect = JsonSerializer.Deserialize<BannerDetectInfo>(doc.detect_info!);
        Assert.NotNull(detect);
        Assert.Contains("Year Price Guarantee", detect!.Text);
        Assert.Equal(0.24996, detect.TimeDetectImage);
    }
}

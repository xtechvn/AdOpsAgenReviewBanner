using AdOpsAgenReviewBanner.Application.Queue;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Sau khi Reviewed lưu Mongo thành công, đẩy message mode=execute_plan vào queue ExecutePlan (RabbitMq:ExecutePlanQueueName).
/// </summary>
public interface IExecutePlanQueuePublisher
{
    Task PublishAfterMongoInsertAsync(
        ExecutePlanPublishRequest request,
        CancellationToken cancellationToken = default);
}

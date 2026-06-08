using System.Text;
using System.Text.Json;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AdOpsAgenReviewBanner.Infrastructure.Messaging;

/// <summary>
/// Reviewed worker → sau insert Mongo → publish JSON mode=execute_plan cho ExecutePlan worker.
/// </summary>
public sealed class RabbitMqExecutePlanQueuePublisher : IExecutePlanQueuePublisher, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly object _sync = new();
    private IConnection? _connection;
    private IModel? _channel;
    private bool _disposed;

    public RabbitMqExecutePlanQueuePublisher(IOptions<RabbitMqSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task PublishAfterMongoInsertAsync(
        ExecutePlanPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.PublishExecutePlanAfterMongoInsert)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(request.CreativeId))
            return Task.CompletedTask;

        try
        {
            var message = ExecutePlanQueueMessageBuilder.FromMongoReview(request);
            var creativeId = request.CreativeId;
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var channel = GetOrCreateChannel();
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            var executePlanQueue = _settings.ExecutePlanQueueName;
            channel.BasicPublish(
                exchange: "",
                routingKey: executePlanQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            Console.WriteLine(
                $"RabbitMQ publish execute_plan OK → creative_id={creativeId}, action={message.Action}, queue={executePlanQueue}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"RabbitMQ publish execute_plan thất bại creative_id={request.CreativeId}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private IModel GetOrCreateChannel()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (_channel is { IsOpen: true })
                return _channel;

            _channel?.Dispose();
            _connection?.Dispose();

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                VirtualHost = _settings.VirtualHost,
                UserName = _settings.UserName,
                Password = _settings.Password,
                Port = _settings.Port
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(
                queue: _settings.ExecutePlanQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            return _channel;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _channel?.Dispose();
            _connection?.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMqExecutePlanQueuePublisher));
    }
}

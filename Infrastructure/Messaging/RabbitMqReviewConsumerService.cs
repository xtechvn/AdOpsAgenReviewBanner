using System.Text;
using System.Text.Json;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AdOpsAgenReviewBanner.Infrastructure.Messaging;

/// <summary>
/// Consumer RabbitMQ — chỉ hoạt động khi Runtime.Environment = Production.
/// Host.RunAsync() trong Program.cs sẽ start service này tự động.
/// Message JSON reviewed: { "link_review": "...", "mode": "reviewed", "order": 5 }
/// Message JSON execute_plan: { "creative_id": "...", "action": "Blocked"|"Reviewed", "mode": "execute_plan" }
/// </summary>
public sealed class RabbitMqReviewConsumerService : BackgroundService
{
    private readonly IOptions<RabbitMqSettings> _rabbitSettings;
    private readonly IOptions<RuntimeSettings> _runtimeSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public RabbitMqReviewConsumerService(
        IOptions<RabbitMqSettings> rabbitSettings,
        IOptions<RuntimeSettings> runtimeSettings,
        IServiceScopeFactory scopeFactory)
    {
        _rabbitSettings = rabbitSettings;
        _runtimeSettings = runtimeSettings;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_runtimeSettings.Value.Environment != RuntimeEnvironment.Production)
            return;

        var settings = _rabbitSettings.Value;
        var workerMode = _runtimeSettings.Value.WorkerMode;
        var consumerQueue = settings.ResolveConsumerQueueName(workerMode);
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
            Port = settings.Port,
            // Bắt buộc với AsyncEventingBasicConsumer — thiếu flag này message có thể unacked mà không chạy handler.
            DispatchConsumersAsync = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: consumerQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.BasicQos(0, settings.PrefetchCount, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            await HandleMessageAsync(channel, ea, stoppingToken);
        };

        channel.BasicConsume(
            queue: consumerQueue,
            autoAck: false,
            consumer: consumer);

        Console.WriteLine(
            $" [*] RabbitMQ connected: {settings.HostName}:{settings.Port} vhost={settings.VirtualHost}");
        Console.WriteLine(
            $" [*] Waiting for messages on queue={consumerQueue}, workerMode={workerMode}");

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
    }

    private async Task HandleMessageAsync(
        IModel channel,
        BasicDeliverEventArgs ea,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received queue message: {messageJson}");

            var message = JsonSerializer.Deserialize<ReviewQueueMessage>(
                messageJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (message is null)
            {
                Console.Error.WriteLine("Message không hợp lệ. Ack bỏ qua.");
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            if (!QueueModeHelper.TryParse(message.Mode, out var messageMode)
                || messageMode != _runtimeSettings.Value.WorkerMode)
            {
                Console.Error.WriteLine(
                    $"Skip message mode={message.Mode}, worker={_runtimeSettings.Value.WorkerMode} — requeue (chạy đúng worker/queue).");
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                return;
            }

            Console.WriteLine(_runtimeSettings.Value.WorkerMode == WorkerMode.ExecutePlan
                ? "Bắt đầu ExecutePlan — mở Chrome GAM, Allow/Block creative."
                : "Bắt đầu Reviewed — mở Chrome GAM, Florence sau khi chụp banner.");

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ReviewQueueMessageProcessor>();
            var result = await processor.ProcessAsync(message, cancellationToken);

            switch (result)
            {
                case QueueProcessResult.SkippedModeMismatch:
                    Console.Error.WriteLine(
                        $"Mode mismatch sau validate — mode={message.Mode}, worker={_runtimeSettings.Value.WorkerMode}");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                    return;
                case QueueProcessResult.InvalidMessage:
                    Console.Error.WriteLine("Message không hợp lệ. Ack bỏ qua.");
                    break;
                case QueueProcessResult.BlockedActionFailed:
                    Console.Error.WriteLine("Blocked action GAM thất bại (creative_id / Selenium).");
                    break;
            }

            channel.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Lỗi xử lý queue message: {ex}");
            channel.BasicAck(ea.DeliveryTag, false);
        }
    }
}

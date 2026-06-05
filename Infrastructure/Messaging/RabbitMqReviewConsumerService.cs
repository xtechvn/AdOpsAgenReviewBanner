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
/// Message JSON: { "link_review": "...", "mode": "reviewed"|"blocked" }
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
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
            Port = settings.Port
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: settings.QueueName,
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
            queue: settings.QueueName,
            autoAck: false,
            consumer: consumer);

        Console.WriteLine(
            $" [*] Waiting for messages on queue={settings.QueueName}, workerMode={_runtimeSettings.Value.WorkerMode}");

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

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ReviewQueueMessageProcessor>();
            var result = await processor.ProcessAsync(message, cancellationToken);

            switch (result)
            {
                case QueueProcessResult.SkippedModeMismatch:
                    Console.WriteLine(
                        $"Skip message mode={message.Mode}, worker={_runtimeSettings.Value.WorkerMode}");
                    break;
                case QueueProcessResult.InvalidMessage:
                    Console.Error.WriteLine("Message không hợp lệ. Ack bỏ qua.");
                    break;
                case QueueProcessResult.FetchImageFailed:
                    Console.Error.WriteLine("Không tải được ảnh từ link_review.");
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

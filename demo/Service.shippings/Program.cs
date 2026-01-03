using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<ShippingQueueListener>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole();
    })
    .Build();

await host.RunAsync();

public sealed class ShippingQueueListener : BackgroundService
{
    private readonly ILogger<ShippingQueueListener> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly Random _random = new();

    public ShippingQueueListener(ILogger<ShippingQueueListener> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: "shipping.command", durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            var payload = Encoding.UTF8.GetString(args.Body.ToArray());
            var correlationId = ResolveCorrelationId(args) ?? Guid.NewGuid().ToString("N");
            var isFailure = _random.Next(5) == 0;

            if (isFailure)
            {
                _logger.LogWarning("Shipping FAILED (CorrelationId: {CorrelationId}): {Body}", correlationId, payload);
                PublishCompletion("Failure", payload, correlationId);
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            else
            {
                _logger.LogInformation("Shipping SUCCESS (CorrelationId: {CorrelationId}): {Body}", correlationId, payload);
                PublishCompletion("Success", payload, correlationId);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }

            await Task.CompletedTask;
        };

        _channel.BasicConsume(queue: "shipping.command", autoAck: false, consumer: consumer);
        _logger.LogInformation("Listening on shipping.command queue...");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private void PublishCompletion(string status, string payload, string correlationId)
    {
        _channel!.QueueDeclare(queue: "orders.command", durable: true, exclusive: false, autoDelete: false);
        using var document = JsonDocument.Parse(payload);

        var envelope = JsonSerializer.Serialize(new
        {
            stepId = "shipping",
            status,
            payload = document.RootElement
        });

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Headers = new Dictionary<string, object>
        {
            ["x-correlation-id"] = Encoding.UTF8.GetBytes(correlationId)
        };

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: "orders.command",
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(envelope));
    }

    private static string? ResolveCorrelationId(BasicDeliverEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.BasicProperties?.CorrelationId))
        {
            return args.BasicProperties.CorrelationId;
        }

        if (args.BasicProperties?.Headers is { } headers &&
            headers.TryGetValue("x-correlation-id", out var value) &&
            value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return null;
    }
}

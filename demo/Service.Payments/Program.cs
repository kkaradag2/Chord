using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<PaymentQueueListener>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole();
    })
    .Build();

await host.RunAsync();

public sealed class PaymentQueueListener : BackgroundService
{
    private readonly ILogger<PaymentQueueListener> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public PaymentQueueListener(ILogger<PaymentQueueListener> logger)
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

        _channel.QueueDeclare(queue: "payment.command", durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        var random = new Random();

        consumer.Received += async (_, args) =>
        {
            var body = Encoding.UTF8.GetString(args.Body.ToArray());
            var correlationId = ResolveCorrelationId(args) ?? Guid.NewGuid().ToString("N");
            var isFailure = random.Next(5) == 0;

            if (isFailure)
            {
                _logger.LogWarning("Payment FAILED (CorrelationId: {CorrelationId}): {Body}", correlationId, body);
                PublishOrderEvent("OrderRollbackRequested", body, correlationId);
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            else
            {
                _logger.LogInformation("Payment SUCCESS (CorrelationId: {CorrelationId}): {Body}", correlationId, body);
                PublishOrderEvent("PaymentCompleted", body, correlationId);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }

            await Task.CompletedTask;
        };

        _channel.BasicConsume(queue: "payment.command", autoAck: false, consumer: consumer);
        _logger.LogInformation("Listening on payment.command queue...");

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private void PublishOrderEvent(string eventName, string originalPayload, string correlationId)
    {
        _channel!.QueueDeclare(queue: "orders.command", durable: true, exclusive: false, autoDelete: false);

        using var payloadDocument = JsonDocument.Parse(originalPayload);
        var eventPayload = JsonSerializer.Serialize(new
        {
            stepId = "payment",
            status = string.Equals(eventName, "PaymentCompleted", StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure",
            payload = payloadDocument.RootElement
        });

        var properties = _channel!.CreateBasicProperties();
        properties.Persistent = true;
        properties.Headers = new Dictionary<string, object>
        {
            ["x-correlation-id"] = Encoding.UTF8.GetBytes(correlationId)
        };

        var body = Encoding.UTF8.GetBytes(eventPayload);
        _channel.BasicPublish(exchange: string.Empty, routingKey: "orders.command", mandatory: false, basicProperties: properties, body: body);
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

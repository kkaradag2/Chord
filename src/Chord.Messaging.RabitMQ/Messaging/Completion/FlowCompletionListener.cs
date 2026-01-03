using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chord.Messaging.RabitMQ.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chord.Messaging.RabitMQ.Messaging.Completion;

internal sealed class FlowCompletionListener : BackgroundService
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IFlowCompletionProcessor _processor;
    private readonly ILogger<FlowCompletionListener> _logger;
    private readonly RabbitMqOptions _options;
    private IModel? _channel;

    public FlowCompletionListener(
        IRabbitMqConnectionProvider connectionProvider,
        IFlowCompletionProcessor processor,
        IOptions<RabbitMqOptions> options,
        ILogger<FlowCompletionListener> logger)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = _connectionProvider.Connection.CreateModel();
        _channel.QueueDeclare(
            queue: _options.CompletionQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var correlationId = ResolveCorrelationId(args);
                if (correlationId is null)
                {
                    _logger.LogWarning("Completion message missing correlation id. Message discarded.");
                    _channel.BasicAck(args.DeliveryTag, multiple: false);
                    return;
                }

                var json = Encoding.UTF8.GetString(args.Body.ToArray());
                var completion = FlowCompletionMessage.Parse(correlationId, json);
                await _processor.ProcessAsync(completion, stoppingToken).ConfigureAwait(false);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process completion message.");
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: _options.CompletionQueueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Flow completion listener started on queue {Queue}.", _options.CompletionQueueName);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
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

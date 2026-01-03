# Chord.Messaging.RabitMQ

This package wires Chord into RabbitMQ queues through the `config.Messaging(...)` configuration hook exposed by `ChordConfigurationBuilder`.

## Usage

```csharp
builder.Services.AddChord(config =>
{
    config.Flow(flow => flow.FromYamlFile("flow/order-flow.yaml"));

    config.Messaging(messaging =>
    {
        messaging.RabbitMq(options =>
        {
            options.HostName = "localhost";
            options.UserName = "guest";
            options.Password = "guest";
        });

        messaging.BindFlow();
    });
});
```

Facts:

- Only a single messaging provider may be registered (RabbitMQ now, Kafka later). Attempting to register multiple providers throws `ChordConfigurationException`.
- Calling `BindFlow()` finalizes the messaging pipeline and verifies at least one provider is configured.
- The package exposes `IChordMessagePublisher` and `IRabbitMqMessagePublisher` for publishing messages to queues.
- Kafka integration is not implemented yet; `messaging.Kafka` throws `NotSupportedException`.

Message publishing:

```csharp
var publisher = app.Services.GetRequiredService<IRabbitMqMessagePublisher>();
await publisher.PublishAsync("order.command", "payload text");
```

## Starting a flow from the host

After calling `m.BindFlow()`, the host application gains access to `IChordFlowMessenger`. The first workflow step is assumed to belong to the host itself, therefore the messenger publishes the supplied payload to the queue defined in the second step.

```csharp
var messenger = app.Services.GetRequiredService<IChordFlowMessenger>();

await messenger.StartAsync("""
{
    "orderId": 1234,
    "total": 50.00
}
""");
```

If the workflow contains fewer than two steps, or if `BindFlow()` is omitted, Chord throws `ChordConfigurationException` during startup to protect the pipeline configuration.

Every flow invocation automatically tags the outgoing RabbitMQ message with a generated `x-correlation-id` header so downstream services can trace the execution chain.

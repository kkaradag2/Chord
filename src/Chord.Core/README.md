# Chord.Core

## Registering the core services

Chord is activated inside a host application by extending the service collection:

```csharp
builder.Services.AddChord(options =>
{
    options
        .UseYamlFlows("flows/sample.yaml")
        .UseRabbitMq(rabbit =>
        {
            rabbit.HostName = "localhost";
            rabbit.Port = 5672;
            rabbit.UserName = "guest";
            rabbit.Password = "guest";
            rabbit.VirtualHost = "/";
        });
});
```

`ChordOptions` currently exposes the `UseYamlFlows` helper, allowing multiple YAML files to be registered through repeated calls or the `params` overload:

```csharp
options.UseYamlFlows("flows/sample.yaml", "flows/billing.yaml");
```

Each YAML file is parsed during startup and the `flow.name` declared inside must be unique across the application; duplicates trigger a `ChordConfigurationException` before any flows are executed. The parsed metadata (flow name, version, orchestrator queues, and step command queues) is cached in memory so downstream components can read it without reopening the original YAML files.

Exactly one messaging provider must be selected (`UseRabbitMq` or `UseKafka`) so that Chord can fail-fast when host wiring is incomplete. Future messaging, storage and telemetry packages will extend `ChordOptions` with their own helpers to keep host wiring centralized.

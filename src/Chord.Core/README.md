# Chord.Core

## Registering the core services

Chord is activated inside a host application by extending the service collection (assuming the host references `Chord.Core`, `Chord.Messaging.RabbitMQ`, and the desired store package such as `Chord.Store.InMemory`):

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
        })
        .UseInMemoryStore();
});
```

`ChordOptions` currently exposes the `UseYamlFlows` helper, allowing multiple YAML files to be registered through repeated calls or the `params` overload:

```csharp
options.UseYamlFlows("flows/sample.yaml", "flows/billing.yaml");
```

Each YAML file is parsed during startup and the `flow.name` declared inside must be unique across the application; duplicates trigger a `ChordConfigurationException` before any flows are executed. The parsed metadata (flow name, version, orchestrator queues, and step command queues) is cached in memory so downstream components can read it without reopening the original YAML files.

Exactly one messaging provider must be selected (`UseRabbitMq` or `UseKafka`) so that Chord can fail-fast when host wiring is incomplete. Future messaging, storage and telemetry packages will extend `ChordOptions` with their own helpers to keep host wiring centralized.

## Starting flows

The runtime is exposed via `ChordFlowRuntime`. Inject it into your application service and call `StartFlowAsync` with the flow name declared in YAML:

```csharp
public sealed class OrderController : ControllerBase
{
    private readonly ChordFlowRuntime _runtime;

    public OrderController(ChordFlowRuntime runtime) => _runtime = runtime;

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(OrderPayload payload)
    {
        await _runtime.StartFlowAsync("OrderFlow", payload);
        return Accepted();
    }
}
```

The runtime caches flow metadata from `UseYamlFlows`, dispatches the first step, listens to the orchestrator completion queue, and routes payloads through the remaining steps until the flow reaches `Completed`.

## Flow state tracking

Each invocation of `StartFlowAsync` creates a flow instance entry through the configured `IChordStore`. The store records the flow name, correlation id, current status, and timestamps for `startedAt`/`completedAt` including the total duration (in milliseconds). When a step is dispatched the runtime immediately creates a step instance row, capturing the `stepId`, associated flow instance id, status, and timestamps. Upon receiving the completion event the runtime marks the step (and eventually the flow) as `Completed`, filling the completion timestamps and duration. Chord.Core never talks to a physical database directly; it reaches the configured store implementation via the `IChordStore` abstraction so providers such as PostgreSQL or in-memory stores can keep the orchestration state in sync.

The runtime also captures outbound and inbound orchestration traffic through the `chord_message_logs` table (or in-memory equivalent). Every publish call records the destination queue, headers, and timestamps, and every completion message that flows through the orchestrator listener is persisted with its correlation metadata. Providers can expose these logs for auditing, troubleshooting, or replay capabilities without re-reading YAML files.

## Choosing a store

Chord requires exactly one state store. For development or unit testing reference `Chord.Store.InMemory` and call `UseInMemoryStore()`. For production-grade persistence install `Chord.Store.PostgreSql`, reference `Chord.Store.PostgreSql` from your host, and configure it through `UsePostgreSqlStore(opts => { ... })`. The PostgreSQL option validates connection details and automatically applies the embedded schema scripts during `AddChord`, ensuring tables are created without EF Core migrations.

# Chord.Core

Chord.Core provides the orchestration primitives that host applications use to describe and register Chord workflows. The library exposes an `AddChord` extension method so you can plug workflow definitions into ASP.NET Core (or any `IHost`) using a single YAML file.

## Registering Chord in a host

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChord(config =>
{
    config.Flow(flow =>
    {
        flow.FromYamlFile("flow/order-flow.yaml");
    });
});
```

Key points:

- `FromYamlFile` accepts relative or absolute paths. The file must exist when the host starts; otherwise a `FileNotFoundException` is thrown.
- Only `.yaml` and `.yml` extensions are allowed. Any other extension results in a `ChordConfigurationException`.
- YAML parsing/validation happens during startup, meaning misconfigurations fail fast and prevent the application from starting in an unsafe state.

## Supported YAML schema

```yaml
workflow:
  name: OrderFlow
  version: 1.0

steps:
  - id: order
    service: OrderService
    command:
      event: OrderRequested
      queue: order.command
    completion:
      event: OrderCompleted
      timeout: 5s

  - id: payment
    service: PaymentService
    command:
      event: PaymentRequested
      queue: payment.command
    completion:
      event: PaymentCompleted
      timeout: 10s
    onFailure:
      rollback:
        - step: order
          command:
            event: OrderRollbackRequested
            queue: order.command

  - id: shipping
    service: ShippingService
    command:
      event: ShippingRequested
      queue: shipping.command
    completion:
      event: ShippingCompleted
      timeout: 10s
    onFailure:
      rollback:
        - step: payment
          command:
            event: PaymentRollbackRequested
            queue: payment.command
        - step: order
          command:
            event: OrderRollbackRequested
            queue: order.command
```

## Validation rules

`FromYamlFile` enforces several safety checks. Any violation throws a `ChordConfigurationException` with a descriptive English message (unless stated otherwise).

- **File existence**: the specified path must point to a real file. Missing files raise `FileNotFoundException`.
- **Extension**: only `.yaml`/`.yml` are accepted to prevent accidentally pointing at unsupported formats.
- **Structure**: the root `workflow` metadata must include both `name` and `version`.
- **Steps count**: every workflow must define at least two steps to avoid trivial or incomplete flows.
- **Step integrity**: each step requires an `id`, `service`, `command`, and `completion`. Command/Completion sections must specify `event` and `queue`/`timeout` fields, respectively.
- **Rollback semantics**: if `onFailure` is present, every rollback entry must reference a `step` and a `command`.
- **YAML syntax**: malformed YAML produces a readable error mentioning that the document contains invalid YAML.

Refer to `test/SamplesYamls` for ready-made valid and invalid YAML examples that mirror the automated tests.

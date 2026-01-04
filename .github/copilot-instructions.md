# Copilot instructions — Chord

Purpose: give AI coding agents the minimal, factual context and explicit patterns needed to be productive in this repository.

## Quick summary
- Project type: .NET 9 (net9.0) multi-project solution (Chord.sln) with a core library, provider-style adapters and a small demo Web API.
- Key folders: `src/Chord.Core/`, `src/Chord.Messaging.*/`, `src/Chord.Store.*/`, `demo/Service.Order/`, `test/`.

## Big picture (what to know first)
- Chord is a flow/coordination library (see top-level `README.md`). The repository separates responsibilities by project: Core (flow model + config), Messaging adapters (RabbitMQ implemented, Kafka placeholder), Storage adapters (InMemory, PostgreSQL placeholders).
- Naming pattern: "Chord.<Area>.<Provider>" (e.g., `Chord.Messaging.RabitMQ`, `Chord.Store.PostgreSql`). Add new providers following that pattern.
- Flows/config appear to use YAML (project includes YamlDotNet). Look for YAML-based flow definitions or add one under an appropriate folder.

## How to build / run / test (exact commands)
- Restore & build solution: `dotnet restore ./Chord.sln` then `dotnet build ./Chord.sln` (requires .NET 9 SDK).
- Run demo Web API: `dotnet run --project demo/Service.Order` (or open `demo/Service.Order` in VS/VSCode and use launch profile in `Properties/launchSettings.json`).
- Tests: there is a `test/Chord.Test.Registrations` folder but no active test project files; use `dotnet test` after you add/enable test projects.

## Project-specific conventions and patterns
- Target framework and conventions:
  - All projects target `net9.0`, use `ImplicitUsings` and `Nullable` enabled (see each `.csproj`).
  - Projects include `PackageReadmeFile: README.md` — add a short README when you add a new project.
- When adding a new adapter/provider:
  - Create `src/Chord.<Area>.<Provider>` and mirror pattern of existing csproj files: reference `Chord.Core` via a `<ProjectReference>`.
  - If the provider needs external packages, add them as `<PackageReference>` (see `Chord.Messaging.RabitMQ.csproj` for `RabbitMQ.Client`).
  - Add a `README.md` explaining configuration keys and any runtime requirements (e.g., RabbitMQ host/credentials).
- Configuration and wiring:
  - Look for code that exposes `config.Messaging(m => m.Kafka(...))` style wiring (Kafka README contains this example); follow this convention for new messaging providers.
  - Use `Microsoft.Extensions.*` patterns for DI and options (these packages appear in csproj files).

## Integration points & runtime dependencies
- RabbitMQ: `src/Chord.Messaging.RabitMQ` references `RabbitMQ.Client`; to test local integrations, run an instance of RabbitMQ and set connection values (the repo currently has no concrete appsettings keys shown in source — add documented keys in provider README).
- PostgreSQL: `src/Chord.Store.PostgreSql` exists as a project but does not contain NuGet DB packages; add `Npgsql` and connection-string guidance if implementing.
- YAML flows: projects depend on `YamlDotNet` — look for YAML flow files or create a `flows/` folder for examples.

## Code style & PR guidance for AI agents
- Keep changes small and focused: add tests and README entries alongside functional changes.
- Follow naming consistency: project and namespace names follow `Chord.<Area>.<Provider>`.
- Update `Chord.sln` to include any new project and add `ProjectReference` from the project that needs it.

## Files to inspect for examples
- `src/Chord.Messaging.RabitMQ/Chord.Messaging.RabitMQ.csproj` — shows RabbitMQ package usage and project reference to `Chord.Core`.
- `src/Chord.Messaging.Kafka/README.md` — placeholder that documents the expected wiring: `config.Messaging(m => m.Kafka(...))`.
- `demo/Service.Order/Program.cs` and `Properties/launchSettings.json` — minimal Web API demo and run profiles.
- `Chord.sln` — solution layout and projects membership.

## What agents should NOT assume
- The repository is a fully implemented product; several projects are placeholders (Kafka, some stores). Do not assume database or messaging integration exists unless you can find implementation and tests.
- There are few (or no) tests — adding tests when adding behavior is expected and helps validation.

---
Feedback? If any area is unclear or you'd like more detail (examples for wiring a new messaging provider, where to add YAML flow examples, or suggested tests), tell me which section and I'll update the file.
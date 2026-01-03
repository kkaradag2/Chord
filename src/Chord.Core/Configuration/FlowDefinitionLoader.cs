using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chord.Core.Exceptions;
using Chord.Core.Flows;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Chord.Core.Configuration;

/// <summary>
/// Loads and validates Chord workflow definitions from YAML sources.
/// </summary>
internal static class FlowDefinitionLoader
{
    /// <summary>
    /// Reads, validates, and converts a YAML workflow file into a <see cref="ChordFlowDefinition"/>.
    /// </summary>
    /// <param name="filePath">Path to the YAML file that describes the workflow.</param>
    /// <returns>The normalized workflow definition ready for runtime consumption.</returns>
    /// <exception cref="ChordConfigurationException">Thrown when the file is missing, malformed, or fails validation.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file cannot be found.</exception>
    public static ChordFlowDefinition FromYamlFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ChordConfigurationException("Workflow file path cannot be empty.");
        }

        var normalizedPath = Path.GetFullPath(filePath);
        EnsureFileIsYaml(normalizedPath);

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException($"Workflow file '{normalizedPath}' was not found.", normalizedPath);
        }

        var yamlContent = File.ReadAllText(normalizedPath);
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new ChordConfigurationException($"Workflow file '{normalizedPath}' is empty.");
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var document = deserializer.Deserialize<WorkflowDocument>(yamlContent)
                           ?? throw new ChordConfigurationException($"Workflow file '{normalizedPath}' has an unexpected structure.");

            ValidateDocument(document, normalizedPath);

            return Map(document);
        }
        catch (YamlException yamlEx)
        {
            throw new ChordConfigurationException($"Workflow file '{normalizedPath}' contains invalid YAML.", yamlEx);
        }
    }

    /// <summary>
    /// Ensures the target file has a recognized YAML extension.
    /// </summary>
    private static void EnsureFileIsYaml(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension) ||
            !extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ChordConfigurationException($"Workflow file '{filePath}' must have a .yaml or .yml extension.");
        }
    }

    /// <summary>
    /// Validates the deserialized YAML document structure against Chord's workflow requirements.
    /// </summary>
    private static void ValidateDocument(WorkflowDocument document, string filePath)
    {
        if (document.Workflow is null)
        {
            throw new ChordConfigurationException($"Workflow metadata is missing in '{filePath}'.");
        }

        if (string.IsNullOrWhiteSpace(document.Workflow.Name))
        {
            throw new ChordConfigurationException("Workflow name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(document.Workflow.Version))
        {
            throw new ChordConfigurationException("Workflow version cannot be empty.");
        }

        if (document.Steps is null || document.Steps.Count < 2)
        {
            throw new ChordConfigurationException("Workflow must declare at least two steps.");
        }

        for (var i = 0; i < document.Steps.Count; i++)
        {
            var step = document.Steps[i];
            var label = GetStepLabel(step, i);

            if (string.IsNullOrWhiteSpace(step.Id))
            {
                throw new ChordConfigurationException($"Step {label} is missing an 'id'.");
            }

            if (string.IsNullOrWhiteSpace(step.Service))
            {
                throw new ChordConfigurationException($"Step {label} is missing a 'service'.");
            }

            ValidateCommand(step.Command, $"Step {label} command");
            ValidateCompletion(step.Completion, $"Step {label} completion");
            ValidateFailure(step.OnFailure, label);
        }
    }

    /// <summary>
    /// Ensures a command section is present and contains the necessary fields.
    /// </summary>
    private static void ValidateCommand(CommandDocument? command, string prefix)
    {
        if (command is null)
        {
            throw new ChordConfigurationException($"{prefix} section is missing.");
        }

        if (string.IsNullOrWhiteSpace(command.Event))
        {
            throw new ChordConfigurationException($"{prefix} must define an 'event'.");
        }

        if (string.IsNullOrWhiteSpace(command.Queue))
        {
            throw new ChordConfigurationException($"{prefix} must define a 'queue'.");
        }
    }

    /// <summary>
    /// Ensures a completion section is present and contains the necessary fields.
    /// </summary>
    private static void ValidateCompletion(CompletionDocument? completion, string prefix)
    {
        if (completion is null)
        {
            throw new ChordConfigurationException($"{prefix} section is missing.");
        }

        if (string.IsNullOrWhiteSpace(completion.Event))
        {
            throw new ChordConfigurationException($"{prefix} must define an 'event'.");
        }

        if (string.IsNullOrWhiteSpace(completion.Timeout))
        {
            throw new ChordConfigurationException($"{prefix} must define a 'timeout'.");
        }
    }

    /// <summary>
    /// Validates optional failure handling instructions if provided.
    /// </summary>
    private static void ValidateFailure(FailureDocument? failure, string label)
    {
        if (failure is null)
        {
            return;
        }

        if (failure.Rollback is null || failure.Rollback.Count == 0)
        {
            throw new ChordConfigurationException($"Step {label} onFailure must define at least one rollback action.");
        }

        for (var i = 0; i < failure.Rollback.Count; i++)
        {
            var rollback = failure.Rollback[i];
            if (string.IsNullOrWhiteSpace(rollback.Step))
            {
                throw new ChordConfigurationException($"Step {label} rollback #{i + 1} is missing a 'step' reference.");
            }

            ValidateCommand(rollback.Command, $"Step {label} rollback '{rollback.Step}' command");
        }
    }

    /// <summary>
    /// Produces a friendly label for logging/exception messages.
    /// </summary>
    private static string GetStepLabel(WorkflowStepDocument step, int index) =>
        string.IsNullOrWhiteSpace(step.Id) ? $"#{index + 1}" : $"'{step.Id}'";

    /// <summary>
    /// Maps the raw YAML document to runtime DTOs.
    /// </summary>
    private static ChordFlowDefinition Map(WorkflowDocument document)
    {
        var steps = document.Steps!
            .Select(MapStep)
            .ToList();

        return new ChordFlowDefinition(
            document.Workflow!.Name!,
            document.Workflow.Version!,
            steps);
    }

    /// <summary>
    /// Converts a single step document to a runtime definition.
    /// </summary>
    private static ChordFlowStep MapStep(WorkflowStepDocument step)
    {
        var rollbacks = step.OnFailure?.Rollback?
                            .Select(rb => new FlowRollback(
                                rb.Step!,
                                new FlowCommand(rb.Command!.Event!, rb.Command.Queue!)))
                            .ToList()
                        ?? new List<FlowRollback>();

        return new ChordFlowStep(
            step.Id!,
            step.Service!,
            new FlowCommand(step.Command!.Event!, step.Command.Queue!),
            new FlowCompletion(step.Completion!.Event!, step.Completion.Timeout!, step.Completion.Queue),
            rollbacks);
    }
    
    private sealed class WorkflowDocument
    {
        public WorkflowMetadata? Workflow { get; init; }
        public List<WorkflowStepDocument>? Steps { get; init; }
    }

    private sealed class WorkflowMetadata
    {
        public string? Name { get; init; }
        public string? Version { get; init; }
    }

    private sealed class WorkflowStepDocument
    {
        public string? Id { get; init; }
        public string? Service { get; init; }
        public CommandDocument? Command { get; init; }
        public CompletionDocument? Completion { get; init; }
        public FailureDocument? OnFailure { get; init; }
    }

    private sealed class CommandDocument
    {
        public string? Event { get; init; }
        public string? Queue { get; init; }
    }

    private sealed class CompletionDocument
    {
        public string? Event { get; init; }
        public string? Timeout { get; init; }
        public string? Queue { get; init; }
    }

    private sealed class FailureDocument
    {
        public List<RollbackDocument>? Rollback { get; init; }
    }

    private sealed class RollbackDocument
    {
        public string? Step { get; init; }
        public CommandDocument? Command { get; init; }
    }
}

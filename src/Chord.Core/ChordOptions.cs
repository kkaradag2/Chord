using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Chord;

/// <summary>
/// Represents the configuration surface that host applications can use to wire Chord.
/// </summary>
public sealed class ChordOptions
{
    private const string MessagingResourcePath = "(messaging)";
    private static readonly string[] AllowedExtensions = [".yaml", ".yml"];
    private readonly List<YamlFlowRegistration> _yamlFlows = new();
    private readonly List<MessagingProviderRegistration> _messagingProviders = new();
    private IServiceCollection? _services;

    /// <summary>
    /// Gets the YAML-based flow registrations that should be loaded when Chord starts.
    /// </summary>
    public IReadOnlyCollection<YamlFlowRegistration> YamlFlows => _yamlFlows;

    internal IReadOnlyCollection<MessagingProviderRegistration> MessagingProviders => _messagingProviders;

    /// <summary>
    /// Registers one or more YAML files or directories that contain flow definitions.
    /// </summary>
    /// <param name="resourcePaths">Paths to YAML files or directories.</param>
    public ChordOptions UseYamlFlows(params string[] resourcePaths)
    {
        ArgumentNullException.ThrowIfNull(resourcePaths);

        if (resourcePaths.Length == 0)
        {
            throw new ArgumentException("At least one flow path must be provided.", nameof(resourcePaths));
        }

        foreach (var rawPath in resourcePaths)
        {
            RegisterFlowPath(rawPath);
        }

        return this;
    }

    private void RegisterFlowPath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ChordConfigurationException("(empty)", "Flow path cannot be null or whitespace.");
        }

        var fullPath = Path.GetFullPath(rawPath);

        if (Directory.Exists(fullPath))
        {
            RegisterDirectory(fullPath);
            return;
        }

        RegisterFile(fullPath);
    }

    private void RegisterDirectory(string directoryPath)
    {
        string[] yamlFiles;

        try
        {
            yamlFiles = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(HasYamlExtension)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ChordConfigurationException(directoryPath, "Flow directory cannot be read.", ex);
        }

        if (yamlFiles.Length == 0)
        {
            throw new ChordConfigurationException(directoryPath, "Directory does not contain any YAML flow files.");
        }

        foreach (var file in yamlFiles)
        {
            RegisterFile(file);
        }
    }

    private void RegisterFile(string filePath)
    {
        EnsureYamlExtension(filePath);

        if (!File.Exists(filePath))
        {
            throw new ChordConfigurationException(filePath, "Flow file does not exist.");
        }

        string contents;
        try
        {
            contents = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ChordConfigurationException(filePath, "Flow file cannot be read.", ex);
        }

        ChordYamlSchemaValidator.Validate(filePath, contents);
        AddValidatedFlow(filePath);
    }

    /// <summary>
    /// Registers a messaging provider and allows it to contribute dependencies to the service collection.
    /// </summary>
    /// <param name="providerName">The logical name of the provider (e.g. RabbitMQ, Kafka).</param>
    /// <param name="configureServices">Callback that registers provider-specific services.</param>
    public ChordOptions RegisterMessagingProvider(string providerName, Action<IServiceCollection> configureServices)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ChordConfigurationException(MessagingResourcePath, "Messaging provider name cannot be null or whitespace.");
        }

        ArgumentNullException.ThrowIfNull(configureServices);

        var services = _services ?? throw new InvalidOperationException("ChordOptions must be used inside IServiceCollection.AddChord to configure messaging providers.");

        configureServices(services);
        _messagingProviders.Add(new MessagingProviderRegistration(providerName));

        return this;
    }

    private static void EnsureYamlExtension(string filePath)
    {
        if (!HasYamlExtension(filePath))
        {
            throw new ChordConfigurationException(filePath, "Flow path must reference a .yaml or .yml file.");
        }
    }

    private static bool HasYamlExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    internal IEnumerable<YamlFlowRegistration> RawYamlFlows => _yamlFlows;

    internal void AddValidatedFlow(string resourcePath)
    {
        _yamlFlows.Add(new YamlFlowRegistration(resourcePath));
    }

    internal void AddMessagingProviderRegistration(string providerName)
    {
        _messagingProviders.Add(new MessagingProviderRegistration(providerName));
    }

    internal void BindServices(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    internal void ReleaseServices()
    {
        _services = null;
    }

    /// <summary>
    /// Represents a YAML flow file registration.
    /// </summary>
    /// <param name="ResourcePath">The path (relative or absolute) to the YAML file.</param>
    public sealed record YamlFlowRegistration(string ResourcePath);

    internal sealed record MessagingProviderRegistration(string ProviderName);
}

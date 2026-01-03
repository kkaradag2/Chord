using System;
using System.IO;
using Chord.Core;
using Chord.Core.Exceptions;
using Chord.Core.Flows;
using Chord.Store.InMemory.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Test.Registrations;

public class FlowConfigurationTests
{
    private static string SamplesDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SamplesYamls"));

    private static string Sample(string fileName) => Path.Combine(SamplesDirectory, fileName);

    /// <summary>
    /// Ensures the happy path loads the flow from YAML and registers it in DI.
    /// </summary>
    [Fact]
    public void AddChord_WithValidYaml_RegistersFlowDefinition()
    {
        var services = new ServiceCollection();

        services.AddChord(config =>
        {
            config.Flow(flow =>
            {
                flow.FromYamlFile(Sample("order-flow.yaml"));
            });
            config.Store(store => store.InMemory());
        });

        using var provider = services.BuildServiceProvider();
        var flowProvider = provider.GetRequiredService<IChordFlowDefinitionProvider>();

        Assert.Equal("OrderFlow", flowProvider.Flow.Name);
        Assert.Equal("1.0", flowProvider.Flow.Version);
        Assert.Equal(3, flowProvider.Flow.Steps.Count);
        Assert.Collection(
            flowProvider.Flow.Steps,
            step => Assert.Equal("order", step.Id),
            step => Assert.Equal("payment", step.Id),
            step => Assert.Equal("shipping", step.Id));
    }

    /// <summary>
    /// Verifies a missing file path surfaces as FileNotFoundException.
    /// </summary>
    [Fact]
    public void AddChord_WhenYamlFileMissing_ThrowsFileNotFound()
    {
        var services = new ServiceCollection();

        Assert.Throws<FileNotFoundException>(() =>
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("missing-flow.yaml")));
                config.Store(store => store.InMemory());
            }));
    }

    /// <summary>
    /// Guards against misconfigured extensions so only .yaml/.yml files are accepted.
    /// </summary>
    [Fact]
    public void AddChord_WhenFileHasWrongExtension_ThrowsConfigurationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("not-yaml.txt")));
                config.Store(store => store.InMemory());
            }));

        Assert.Contains(".yaml", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Confirms unreadable YAML produces a descriptive configuration exception.
    /// </summary>
    [Fact]
    public void AddChord_WhenYamlIsMalformed_ThrowsConfigurationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("malformed.yaml")));
                config.Store(store => store.InMemory());
            }));

        Assert.Contains("invalid YAML", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates workflow metadata (name/version) requirements.
    /// </summary>
    [Fact]
    public void AddChord_WhenWorkflowMetadataInvalid_ThrowsConfigurationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("invalid-metadata.yaml")));
                config.Store(store => store.InMemory());
            }));

        Assert.Contains("Workflow name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures flows must define at least two steps.
    /// </summary>
    [Fact]
    public void AddChord_WhenWorkflowHasInsufficientSteps_ThrowsConfigurationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("invalid-step-count.yaml")));
                config.Store(store => store.InMemory());
            }));

        Assert.Contains("at least two steps", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

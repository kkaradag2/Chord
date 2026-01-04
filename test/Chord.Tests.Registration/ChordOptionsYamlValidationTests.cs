using System;
using System.IO;
using Chord;
using Xunit;

namespace Chord.Tests.Registration;

public class ChordOptionsYamlValidationTests
{
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void UseYamlFlows_Throws_For_NonYamlFile()
    {
        var path = GetFullPath("not-yaml.txt");
        var options = new ChordOptions();

        var ex = Assert.Throws<ChordConfigurationException>(() => options.UseYamlFlows(path));

        Assert.Equal($"Chord configuration error for '{path}': Flow path must reference a .yaml or .yml file.", ex.Message);
    }

    [Fact]
    public void UseYamlFlows_Throws_For_MissingFile()
    {
        var path = Path.Combine(TestDataPath, "missing-flow.yaml");
        var fullPath = Path.GetFullPath(path);
        var options = new ChordOptions();

        var ex = Assert.Throws<ChordConfigurationException>(() => options.UseYamlFlows(fullPath));

        Assert.Equal($"Chord configuration error for '{fullPath}': Flow file does not exist.", ex.Message);
    }

    [Fact]
    public void UseYamlFlows_Throws_For_UnparsableYaml()
    {
        var path = GetFullPath("malformed.yaml");
        var options = new ChordOptions();

        var ex = Assert.Throws<ChordConfigurationException>(() => options.UseYamlFlows(path));

        Assert.Equal($"Chord configuration error for '{path}': Flow file is not a valid YAML document.", ex.Message);
    }

    [Fact]
    public void UseYamlFlows_Throws_For_InvalidChordSchema()
    {
        var path = GetFullPath("invalid-structure.yaml");
        var options = new ChordOptions();

        var ex = Assert.Throws<ChordConfigurationException>(() => options.UseYamlFlows(path));

        Assert.Equal($"Chord configuration error for '{path}': Chord YAML document must declare 'orchestrator' section.", ex.Message);
    }

    [Fact]
    public void UseYamlFlows_Registers_ValidYaml()
    {
        var path = GetFullPath("valid-flow.yaml");
        var options = new ChordOptions();

        options.UseYamlFlows(path);

        var registration = Assert.Single(options.YamlFlows);
        Assert.Equal(path, registration.ResourcePath);
        var flow = registration.Flow;
        Assert.Equal("ValidFlow", flow.Name);
        Assert.Equal("1.0", flow.Version);
        Assert.Equal("completion-queue", flow.CompletionQueue);
        Assert.Equal("failure-queue", flow.FailureQueue);
        var step = Assert.Single(flow.Steps);
        Assert.Equal("reserve", step.Id);
        Assert.Equal("reserve-queue", step.CommandQueue);
    }

    [Fact]
    public void UseYamlFlows_Throws_For_Directory()
    {
        var directoryPath = Path.GetFullPath(TestDataPath);
        var options = new ChordOptions();

        var ex = Assert.Throws<ChordConfigurationException>(() => options.UseYamlFlows(directoryPath));

        Assert.Equal($"Chord configuration error for '{directoryPath}': Flow path must reference a YAML file, not a directory.", ex.Message);
    }

    [Fact]
    public void UseYamlFlows_Throws_For_DuplicateFlowName()
    {
        var first = GetFullPath("valid-flow.yaml");
        var duplicate = GetFullPath("duplicate-flow.yaml");
        var options = new ChordOptions();

        var ex = Assert.Throws<ChordConfigurationException>(() => options.UseYamlFlows(first, duplicate));

        Assert.Equal($"Chord configuration error for '{duplicate}': Flow name 'ValidFlow' is already registered by '{first}'.", ex.Message);
    }

    private static string GetFullPath(string fileName)
    {
        var path = Path.Combine(TestDataPath, fileName);
        return Path.GetFullPath(path);
    }
}

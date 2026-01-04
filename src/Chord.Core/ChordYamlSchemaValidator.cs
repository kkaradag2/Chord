using System;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Chord;

internal static class ChordYamlSchemaValidator
{
    public static void Validate(string resourcePath, string yamlContent)
    {
        var root = Parse(resourcePath, yamlContent);

        var flowNode = RequireMapping(root, "flow", resourcePath, "Chord YAML document must declare 'flow' section.");
        RequireScalar(flowNode, "name", resourcePath, "Chord YAML flow must declare 'name'.");
        RequireScalar(flowNode, "version", resourcePath, "Chord YAML flow must declare 'version'.");

        var orchestratorNode = RequireMapping(root, "orchestrator", resourcePath, "Chord YAML document must declare 'orchestrator' section.");
        RequireScalar(orchestratorNode, "completionQueue", resourcePath, "Chord YAML orchestrator must declare 'completionQueue'.");
        RequireScalar(orchestratorNode, "failureQueue", resourcePath, "Chord YAML orchestrator must declare 'failureQueue'.");

        var stepsNode = RequireSequence(root, "steps", resourcePath, "Chord YAML document must declare 'steps' as a list.");
        foreach (var stepNode in stepsNode)
        {
            if (stepNode is not YamlMappingNode stepMapping)
            {
                throw new ChordConfigurationException(resourcePath, "Each step must be a mapping node.");
            }

            RequireScalar(stepMapping, "id", resourcePath, "Each step must declare 'id'.");
            var commandNode = RequireMapping(stepMapping, "command", resourcePath, "Each step must declare a 'command' section.");
            RequireScalar(commandNode, "queue", resourcePath, "Each step command must declare 'queue'.");
        }
    }

    private static YamlMappingNode Parse(string resourcePath, string yamlContent)
    {
        var yamlStream = new YamlStream();
        using var reader = new StringReader(yamlContent);
        try
        {
            yamlStream.Load(reader);
        }
        catch (YamlException)
        {
            throw new ChordConfigurationException(resourcePath, "Flow file is not a valid YAML document.");
        }

        if (yamlStream.Documents.Count == 0)
        {
            throw new ChordConfigurationException(resourcePath, "Flow file does not contain any YAML documents.");
        }

        if (yamlStream.Documents[0].RootNode is not YamlMappingNode mapping)
        {
            throw new ChordConfigurationException(resourcePath, "Flow file root must be a mapping node.");
        }

        return mapping;
    }

    private static YamlMappingNode RequireMapping(YamlMappingNode parent, string key, string resourcePath, string errorMessage)
    {
        if (!TryGetNode(parent, key, out var node))
        {
            throw new ChordConfigurationException(resourcePath, errorMessage);
        }

        if (node is not YamlMappingNode mapping)
        {
            throw new ChordConfigurationException(resourcePath, errorMessage);
        }

        return mapping;
    }

    private static YamlSequenceNode RequireSequence(YamlMappingNode parent, string key, string resourcePath, string errorMessage)
    {
        if (!TryGetNode(parent, key, out var node))
        {
            throw new ChordConfigurationException(resourcePath, errorMessage);
        }

        if (node is not YamlSequenceNode sequence)
        {
            throw new ChordConfigurationException(resourcePath, errorMessage);
        }

        return sequence;
    }

    private static string RequireScalar(YamlMappingNode parent, string key, string resourcePath, string errorMessage)
    {
        if (!TryGetNode(parent, key, out var node))
        {
            throw new ChordConfigurationException(resourcePath, errorMessage);
        }

        if (node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
        {
            throw new ChordConfigurationException(resourcePath, errorMessage);
        }

        return scalar.Value!;
    }

    private static bool TryGetNode(YamlMappingNode parent, string key, out YamlNode node)
    {
        foreach (var child in parent.Children)
        {
            if (child.Key is YamlScalarNode scalar &&
                scalar.Value is not null &&
                scalar.Value.Equals(key, StringComparison.Ordinal))
            {
                node = child.Value;
                return true;
            }
        }

        node = null!;
        return false;
    }
}

using Chord.Core.Configuration;
using Chord.Core.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Core;

/// <summary>
/// Provides registration helpers so host applications can integrate Chord via <c>IServiceCollection</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChord(this IServiceCollection services, Action<ChordConfigurationBuilder> configure)
    {
        // Ensure the caller provided the DI container instance, otherwise registration cannot proceed.
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Capture developer errors early by requiring the configuration callback.
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new ChordConfigurationBuilder();
        configure(builder);
        var flowDefinition = builder.Build();

        services.AddSingleton(flowDefinition);
        services.AddSingleton<IChordFlowDefinitionProvider>(new ChordFlowDefinitionProvider(flowDefinition));

        return services;
    }
}

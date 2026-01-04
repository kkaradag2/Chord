using System.Linq;
using Chord;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Chord into a host <see cref="IServiceCollection"/>.
/// </summary>
public static class ChordServiceCollectionExtensions
{
    /// <summary>
    /// Adds Chord services and allows the caller to configure the runtime via a fluent builder.
    /// </summary>
    /// <param name="services">The service collection the host is building.</param>
    /// <param name="configure">Optional configuration delegate.</param>
    public static IServiceCollection AddChord(this IServiceCollection services, Action<ChordOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ChordMarkerService)))
        {
            throw new InvalidOperationException("Chord has already been registered in this service collection.");
        }

        services.AddSingleton<ChordMarkerService>();

        var options = new ChordOptions();
        options.BindServices(services);
        try
        {
            configure?.Invoke(options);
        }
        finally
        {
            options.ReleaseServices();
        }

        ValidateMessagingProviderSelection(options);
        ValidateStoreProviderSelection(options);

        var frozenOptions = CloneOptions(options);

        services.TryAddSingleton(frozenOptions);
        services.TryAddSingleton<IOptions<ChordOptions>>(_ => new OptionsWrapper<ChordOptions>(frozenOptions));
        services.TryAddSingleton<ChordFlowRuntime>();
        services.AddHostedService(sp => sp.GetRequiredService<ChordFlowRuntime>());

        return services;
    }

    private static void ValidateMessagingProviderSelection(ChordOptions options)
    {
        var count = options.MessagingProviders.Count;
        if (count == 0)
        {
            throw new ChordConfigurationException("(messaging)", "Exactly one messaging provider must be configured via UseRabbitMq or UseKafka.");
        }

        if (count > 1)
        {
            var names = string.Join(", ", options.MessagingProviders.Select(x => x.ProviderName));
            throw new ChordConfigurationException("(messaging)", $"Exactly one messaging provider must be configured, but {count} were provided ({names}).");
        }
    }

    private static void ValidateStoreProviderSelection(ChordOptions options)
    {
        var count = options.StoreProviders.Count;
        if (count == 0)
        {
            throw new ChordConfigurationException("(store)", "Exactly one store provider must be configured via UseInMemoryStore or UsePostgreSqlStore.");
        }

        if (count > 1)
        {
            var names = string.Join(", ", options.StoreProviders.Select(x => x.ProviderName));
            throw new ChordConfigurationException("(store)", $"Exactly one store provider must be configured, but {count} were provided ({names}).");
        }
    }

    private static ChordOptions CloneOptions(ChordOptions source)
    {
        var copy = new ChordOptions();

        foreach (var registration in source.RawYamlFlows)
        {
            copy.AddValidatedFlow(registration.ResourcePath, registration.Flow);
        }

        foreach (var provider in source.MessagingProviders)
        {
            copy.AddMessagingProviderRegistration(provider.ProviderName);
        }

        foreach (var store in source.StoreProviders)
        {
            copy.AddStoreProviderRegistration(store.ProviderName);
        }

        return copy;
    }

    private sealed class ChordMarkerService
    {
    }
}

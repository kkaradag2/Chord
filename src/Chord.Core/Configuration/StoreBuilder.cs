using System;
using Chord.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Core.Configuration;

/// <summary>
/// Provides a fluent API for registering the persistence provider used by Chord.
/// </summary>
public sealed class StoreBuilder
{
    private readonly IServiceCollection _services;
    private string? _selectedProvider;

    internal StoreBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers a store provider by executing the supplied registration action.
    /// </summary>
    /// <param name="providerName">Logical name of the provider (used for diagnostics).</param>
    /// <param name="registration">Delegate responsible for adding provider services.</param>
    internal void UseProvider(string providerName, Action<IServiceCollection> registration)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be empty.", nameof(providerName));
        }

        if (registration is null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        if (_selectedProvider is not null)
        {
            throw new ChordConfigurationException($"Only one store provider can be registered. '{_selectedProvider}' is already configured.");
        }

        registration(_services);
        _selectedProvider = providerName;
    }

    internal void Validate()
    {
        if (_selectedProvider is null)
        {
            throw new ChordConfigurationException("Chord requires exactly one store provider. Call store.InMemory() or store.PostgreSql(...).");
        }
    }
}

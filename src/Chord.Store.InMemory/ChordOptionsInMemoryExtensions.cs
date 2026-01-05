using Chord;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Store.InMemory;

/// <summary>
/// Provides <see cref="ChordOptions"/> extensions for the in-memory store.
/// </summary>
public static class ChordOptionsInMemoryExtensions
{
    private const string ProviderName = "InMemoryStore";

    public static ChordOptions UseInMemoryStore(this ChordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.RegisterStoreProvider(ProviderName, services =>
        {
            services.AddSingleton<IChordStore, InMemoryChordStore>();
        });
    }
}

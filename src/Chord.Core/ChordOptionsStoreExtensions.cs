using Microsoft.Extensions.DependencyInjection;

namespace Chord;

public static class ChordOptionsStoreExtensions
{
    private const string InMemoryProviderName = "InMemoryStore";

    public static ChordOptions UseInMemoryStore(this ChordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.RegisterStoreProvider(InMemoryProviderName, services =>
        {
            services.AddSingleton<IChordStore, InMemoryChordStore>();
        });
    }
}

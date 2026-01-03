using System;
using Chord.Core.Configuration;
using Chord.Core.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Store.InMemory.Configuration;

/// <summary>
/// Adds support for registering the in-memory store via <c>config.Store(...)</c>.
/// </summary>
public static class StoreBuilderExtensions
{
    public static void InMemory(this StoreBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.UseProvider("InMemory", services =>
        {
            services.AddSingleton<IChordStore, InMemoryChordStore>();
        });
    }
}

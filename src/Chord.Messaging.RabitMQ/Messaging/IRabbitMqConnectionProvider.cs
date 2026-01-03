using System;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Provides access to a lazily initialized RabbitMQ connection, handling disposal on shutdown.
/// </summary>
internal interface IRabbitMqConnectionProvider : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the active RabbitMQ connection, opening it on first access.
    /// </summary>
    IConnection Connection { get; }
}

using System;
using RabbitMQ.Client;

namespace Chord.Messaging.RabitMQ;

internal static class RabbitMqConnectionFactoryBuilder
{
    public static ConnectionFactory Build(RabbitMqOptions cfg)
    {
        var factory = new ConnectionFactory
        {
            HostName = cfg.HostName,
            Port = cfg.Port,
            UserName = cfg.UserName,
            Password = cfg.Password,
            VirtualHost = cfg.VirtualHost,
            DispatchConsumersAsync = true,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
        };

        if (cfg.UseSsl)
        {
            factory.Ssl = new SslOption
            {
                Enabled = true
            };
        }

        return factory;
    }
}

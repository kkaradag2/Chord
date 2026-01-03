using System;
using System.IO;
using System.Threading.Tasks;
using Chord.Core;
using Chord.Messaging.RabitMQ.Configuration;
using Chord.Messaging.RabitMQ.Messaging;
using Chord.Store.InMemory.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Chord.Test.Messaging.Basic;

public sealed class EcommerceFlowMessagingTests
{
    /// <summary>
    /// Builds a host, configures Chord with the e-commerce flow, and dispatches the payload to RabbitMQ.
    /// </summary>
    [RabbitMqFact]
    public async Task FlowMessenger_DispatchesOrderPayload()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();

        var flowPath = Path.Combine(AppContext.BaseDirectory, "flows", "ecommerce-flow.yaml");

        builder.Services.AddChord(config =>
        {
            config.Flow(flow => flow.FromYamlFile(flowPath));
            config.Store(store => store.InMemory());
            config.Messaging(m =>
            {
                m.RabbitMq(options =>
                {
                    options.HostName = "localhost";
                    options.Port = 5672;
                    options.UserName = "guest";
                    options.Password = "guest";
                    options.ClientProvidedName = "Chord.Test.Messaging.Basic";
                });

                m.BindFlow();
            });
        });

        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            var messenger = host.Services.GetRequiredService<IChordFlowMessenger>();
            var payload = """
            {
              "orderId": "ORD-1001",
              "customerId": "CST-42",
              "total": 149.99
            }
            """;

            await messenger.StartAsync(payload);
        }
        finally
        {
            await host.StopAsync();
        }
    }

}

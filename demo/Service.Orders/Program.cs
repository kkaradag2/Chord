using Chord.Core;
using Chord.Store.InMemory.Configuration;
using Chord.Messaging.RabitMQ.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var flowPath = Path.Combine(builder.Environment.ContentRootPath, "flows", "order-flow.yaml");

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
            options.ClientProvidedName = "Service.Orders";
        });

        m.BindFlow();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

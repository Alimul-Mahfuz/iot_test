using Esp32Simulator;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<Esp32Settings>(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

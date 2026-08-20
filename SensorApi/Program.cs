using SensorApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<SensorDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SensorDatabase")));
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddHostedService<MqttIngestionService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
    await database.Database.EnsureCreatedAsync();
}

app.MapGet("/", () => Results.Ok(new { service = "Sensor API", status = "ok" }));

app.MapGet("/api/readings", async (SensorDbContext database, int? limit, CancellationToken cancellationToken) =>
{
    var take = Math.Clamp(limit ?? 100, 1, 1000);
    return await database.Readings
        .AsNoTracking()
        .OrderByDescending(reading => reading.ReadTime)
        .Take(take)
        .ToListAsync(cancellationToken);
});

app.MapGet("/api/readings/latest", async (SensorDbContext database, CancellationToken cancellationToken) =>
    await database.Readings
        .AsNoTracking()
        .OrderByDescending(reading => reading.ReadTime)
        .FirstOrDefaultAsync(cancellationToken));

app.Run();

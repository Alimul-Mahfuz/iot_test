using System.Buffers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace SensorApi;

public sealed class MqttIngestionService(
    IDbContextFactory<SensorDbContext> databaseFactory,
    IOptions<MqttSettings> options,
    ILogger<MqttIngestionService> logger) : BackgroundService
{
    private readonly MqttSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += message => StoreReadingAsync(message, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    var optionsBuilder = new MqttClientOptionsBuilder()
                        .WithClientId(_settings.ClientId)
                        .WithTcpServer(_settings.Host, _settings.Port);

                    if (!string.IsNullOrWhiteSpace(_settings.Username))
                    {
                        optionsBuilder.WithCredentials(_settings.Username, _settings.Password);
                    }

                    await client.ConnectAsync(optionsBuilder.Build(), stoppingToken);
                    await client.SubscribeAsync(_settings.Topic, cancellationToken: stoppingToken);
                    logger.LogInformation("Subscribed to MQTT topic {Topic}", _settings.Topic);
                }

                while (client.IsConnected && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "MQTT connection failed. Retrying in {RetrySeconds} seconds", _settings.RetrySeconds);
                await Task.Delay(TimeSpan.FromSeconds(_settings.RetrySeconds), stoppingToken);
            }
        }

        if (client.IsConnected)
        {
            await client.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    private async Task StoreReadingAsync(MqttApplicationMessageReceivedEventArgs message, CancellationToken stoppingToken)
    {
        try
        {
            var reading = JsonSerializer.Deserialize<SensorReading>(message.ApplicationMessage.Payload.ToArray());
            if (reading is null)
            {
                logger.LogWarning("Ignoring empty sensor message on {Topic}", message.ApplicationMessage.Topic);
                return;
            }

            reading.ReceivedAt = DateTime.UtcNow;
            await using var database = await databaseFactory.CreateDbContextAsync(stoppingToken);
            database.Readings.Add(reading);
            await database.SaveChangesAsync(stoppingToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Ignoring invalid sensor message on {Topic}", message.ApplicationMessage.Topic);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not store sensor message from {Topic}", message.ApplicationMessage.Topic);
        }
    }
}

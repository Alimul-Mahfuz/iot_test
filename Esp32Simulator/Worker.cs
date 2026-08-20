using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Esp32Simulator;

public sealed record SensorPayload(
    float TempRead,
    DateTime ReadTime,
    string Unit,
    float Humidity);

public sealed class Worker(
    IOptions<Esp32Settings> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly Esp32Settings _settings = options.Value;
    private readonly Random _random = new();
    private float _temperature = 20;
    private float _humidity = 31;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ESP32 simulator starting");
        await ConnectToWifiAsync(stoppingToken);

        var mqttFactory = new MqttClientFactory();
        using var mqttClient = mqttFactory.CreateMqttClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!mqttClient.IsConnected)
                {
                    await ConnectToMqttAsync(mqttClient, stoppingToken);
                }

                var payload = ReadSensors();
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_settings.Mqtt.Topic)
                    .WithPayload(JsonSerializer.Serialize(payload))
                    .Build();

                await mqttClient.PublishAsync(message, stoppingToken);
                logger.LogInformation("Published sensor reading to {Topic}", _settings.Mqtt.Topic);

                await Task.Delay(TimeSpan.FromSeconds(_settings.PublishIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "ESP32 MQTT operation failed. Retrying in {RetrySeconds} seconds", _settings.Mqtt.RetrySeconds);
                if (mqttClient.IsConnected)
                {
                    await mqttClient.DisconnectAsync(cancellationToken: CancellationToken.None);
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.Mqtt.RetrySeconds), stoppingToken);
            }
        }

        if (mqttClient.IsConnected)
        {
            await mqttClient.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    private async Task ConnectToWifiAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Connecting to Wi-Fi network {Ssid}...", _settings.Wifi.Ssid);
        await Task.Delay(250, stoppingToken);
        logger.LogInformation("Wi-Fi connected. IP address: simulated");
    }

    private async Task ConnectToMqttAsync(IMqttClient mqttClient, CancellationToken stoppingToken)
    {
        var mqtt = _settings.Mqtt;
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(mqtt.ClientId)
            .WithTcpServer(mqtt.Host, mqtt.Port);

        if (!string.IsNullOrWhiteSpace(mqtt.Username))
        {
            optionsBuilder.WithCredentials(mqtt.Username, mqtt.Password);
        }

        await mqttClient.ConnectAsync(optionsBuilder.Build(), stoppingToken);
        logger.LogInformation("MQTT connected to {Host}:{Port}", mqtt.Host, mqtt.Port);
    }

    private SensorPayload ReadSensors()
    {
        _temperature = Math.Clamp(_temperature + NextFloat(-1.5f, 1.5f), -40, 85);
        _humidity = Math.Clamp(_humidity + NextFloat(-0.8f, 0.8f), 0, 100);

        return new SensorPayload(
            MathF.Round(_temperature + NextFloat(-0.1f, 0.1f), 2),
            DateTime.UtcNow,
            "C",
            MathF.Round(_humidity + NextFloat(-0.1f, 0.1f), 2));
    }

    private float NextFloat(float min, float max) =>
        min + ((float)_random.NextDouble() * (max - min));
}

namespace SensorApi;

public sealed class MqttSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Topic { get; set; } = "sensors/temperature";
    public string ClientId { get; set; } = "sensor-api";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int RetrySeconds { get; set; } = 5;
}

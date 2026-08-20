namespace Esp32Simulator;

public sealed class Esp32Settings
{
    public WifiSettings Wifi { get; set; } = new();
    public MqttSettings Mqtt { get; set; } = new();
    public int PublishIntervalSeconds { get; set; } = 1;
}

public sealed class WifiSettings
{
    public string Ssid { get; set; } = "demo-wifi";
}

public sealed class MqttSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Topic { get; set; } = "sensors/temperature";
    public string ClientId { get; set; } = "esp32-simulator";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int RetrySeconds { get; set; } = 5;
}

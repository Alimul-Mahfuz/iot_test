namespace SensorApi;

public sealed class SensorReading
{
    public long Id { get; set; }
    public float TempRead { get; set; }
    public float Humidity { get; set; }
    public DateTime ReadTime { get; set; }
    public string Unit { get; set; } = "C";
    public DateTime ReceivedAt { get; set; }
}

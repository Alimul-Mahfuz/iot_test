using Microsoft.AspNetCore.SignalR;

namespace SensorApi;

public sealed class SensorHub : Hub
{
    public const string ReadingReceivedEvent = "ReadingReceived";
}

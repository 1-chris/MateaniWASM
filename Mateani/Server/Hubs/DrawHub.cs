using Mateani.Shared.Classes;
using Microsoft.AspNetCore.SignalR;

namespace Mateani.Server.Hubs;

public class DrawHub : Hub
{
    public async Task SendCommand(DrawingCommand command)
    {
        // Console.WriteLine($"Server received command with color: {command.Color:X8}");
        await Clients.All.SendAsync("ReceiveCommand", command);
    }
}
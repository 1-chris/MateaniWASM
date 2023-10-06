using Mateani.Shared.Classes;
using Microsoft.AspNetCore.SignalR;

namespace Mateani.Server.Hubs;

public class DrawHub : Hub
{
    private static List<DrawingCommand> _commands = new();
    public async Task SendCommand(DrawingCommand command)
    {
        // Console.WriteLine($"Server received command with color: {command.Color:X8}");
        _commands.Add(command);
        await Clients.Others.SendAsync("ReceiveCommand", command);
    }

    public override async Task OnConnectedAsync()
    {
        if (_commands.Count > 0)
        {
            foreach (var command in _commands)
            {
                await Clients.Caller.SendAsync("ReceiveCommand",command);
            }
        }
        
        await base.OnConnectedAsync();
    }
}
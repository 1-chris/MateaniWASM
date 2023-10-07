using System.Collections.Concurrent;
using Mateani.Shared.Classes;
using Microsoft.AspNetCore.SignalR;
using SkiaSharp;
using Mateani.Server.Services;

namespace Mateani.Server.Hubs;

public class DrawHub : Hub
{
    private readonly DrawGroupManager _groupManager;
    
    private static SemaphoreSlim _semaphoreSlim = new(1, 1);
    private static List<DrawingCommand> _commands = new();
    private static byte[] _cachedImage;
    private static ConcurrentDictionary<string, byte[]> _groupImages = new();
    private static ConcurrentDictionary<string, ConcurrentQueue<DrawingCommand>> _groupDrawCommands = new();

    public DrawHub(DrawGroupManager groupManager)
    {
        _groupManager = groupManager;
    }
    
    
    public async Task SendCommand(string groupName, DrawingCommand command)
    {
        var commands = _groupDrawCommands.GetOrAdd(groupName, new ConcurrentQueue<DrawingCommand>());
        commands.Enqueue(command);
        
        await Clients.GroupExcept(groupName, Context.ConnectionId).SendAsync("ReceiveCommand", command);
    }


    public async Task JoinGroup(string groupName)
    {
        _groupManager.AddToGroup(Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        // skiasharp library makes things weird with asp.net
        await _semaphoreSlim.WaitAsync();  
        try
        {
            byte[] cachedImage;
            ConcurrentQueue<DrawingCommand> groupCommands;
            _groupImages.TryGetValue(groupName, out cachedImage);
            _groupDrawCommands.TryGetValue(groupName, out groupCommands);
            
            if (cachedImage?.Length > 0)
            {
                await Clients.Caller.SendAsync("ReceiveImage", cachedImage);
            }
            
            if (groupCommands?.Count > 100)
            {
                UpdateCachedGroupImage(groupName);
                _groupImages.TryGetValue(groupName, out cachedImage);
                await Clients.Caller.SendAsync("ReceiveImage", cachedImage);
                groupCommands.Clear();
            }

            if (groupCommands?.Count > 0)
            {
                foreach (var command in groupCommands)
                {
                    await Clients.Caller.SendAsync("ReceiveCommand", command);
                }
            }
        }
        finally
        {
            _semaphoreSlim.Release();  
        }
    }

    public async Task LeaveGroup(string groupName)
    {
        _groupManager.RemoveFromGroup(Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
    
    private void UpdateCachedGroupImage(string groupName)
    {
        var cachedImage = _groupImages.GetValueOrDefault(groupName);
        var groupCommands = _groupDrawCommands.GetValueOrDefault(groupName);
        
        if (cachedImage == null)
        {
            using (SKImage image = CreateImageFromCommands(groupCommands))
            {
                using (var encodedImage = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    _groupImages.AddOrUpdate(groupName, encodedImage.ToArray(), (_, image) =>
                    {
                        image = encodedImage.ToArray();
                        return image;
                    });
                }
            }

            Console.WriteLine($"null image, cached image created for {groupName}");
        }
        else
        {
            using (var stream = new MemoryStream(cachedImage))
            using (var bitmap = SKBitmap.Decode(stream))
            using (var canvas = new SKCanvas(bitmap))
            {
                var commands = _groupDrawCommands.GetValueOrDefault(groupName);
                foreach (var command in _commands)
                {
                    using var paint = new SKPaint
                    {
                        Color = command.Color,
                        StrokeWidth = command.Size
                    };
                    canvas.DrawCircle(command.X, command.Y, command.Size, paint);
                }

                using (var image = SKImage.FromBitmap(bitmap))
                using (var encodedImage = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    var updateSuccess = _groupImages.TryUpdate(groupName, encodedImage.ToArray(), cachedImage);
                    Console.WriteLine($"update success: {updateSuccess}");
                }
            }
            Console.WriteLine($"existing image, cached image updated for {groupName}");
        }
    }
    


    private SKImage CreateImageFromCommands(ConcurrentQueue<DrawingCommand> commands)
    {
        int width = 1000;
        int height = 840;
        var info = new SKImageInfo(width, height);

        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Bisque);

        foreach (var command in commands)
        {
            using var paint = new SKPaint
            {
                Color = command.Color,
                StrokeWidth = command.Size
            };
            canvas.DrawCircle(command.X, command.Y, command.Size, paint);
        }

        return SKImage.FromBitmap(bitmap);
    }
}
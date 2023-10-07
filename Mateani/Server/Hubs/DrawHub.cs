using System.Collections.Concurrent;
using Mateani.Shared.Classes;
using Microsoft.AspNetCore.SignalR;
using SkiaSharp;
using Mateani.Server.Services;

namespace Mateani.Server.Hubs;

public class DrawHub : Hub
{
    private readonly DrawGroupManager _groupManager;
    
    private static ConcurrentDictionary<string, byte[]> _groupImages = new();
    private static ConcurrentDictionary<string, ConcurrentQueue<DrawingCommand>> _groupDrawCommands = new();

    private static SemaphoreSlim _semaphoreSlim = new(1, 1);

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
            _groupDrawCommands.TryGetValue(groupName, out ConcurrentQueue<DrawingCommand> groupCommands);
            
            if (groupCommands?.Count > 0)
            {
                UpdateCachedGroupImage(groupName);
                groupCommands.Clear();
            }

            _groupImages.TryGetValue(groupName, out var cachedImage);
            await Clients.Caller.SendAsync("ReceiveImage", cachedImage);
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

        if (groupCommands.Count == 0) return; // no updates required
        
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
                if (groupCommands.Count == 0) return;
                
                foreach (var command in groupCommands)
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
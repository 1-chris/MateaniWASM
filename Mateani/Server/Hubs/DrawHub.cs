using Mateani.Shared.Classes;
using Microsoft.AspNetCore.SignalR;
using SkiaSharp;

namespace Mateani.Server.Hubs;

public class DrawHub : Hub
{
    private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    private static List<DrawingCommand> _commands = new();
    private static byte[] _cachedImage;
    public async Task SendCommand(DrawingCommand command)
    {
        // Console.WriteLine($"Server received command with color: {command.Color:X8}");
        _commands.Add(command);
        await Clients.Others.SendAsync("ReceiveCommand", command);
    }

    public override async Task OnConnectedAsync()
    {
        await _semaphoreSlim.WaitAsync();  
        try
        {
            if (_cachedImage?.Length > 0)
            {
                await Clients.Caller.SendAsync("ReceiveImage", _cachedImage);
            }
            
            if (_commands.Count > 100)
            {
                UpdateCachedImage();
                await Clients.Caller.SendAsync("ReceiveImage", _cachedImage);
                _commands.Clear();
            }

            if (_commands.Count > 0)
            {
                foreach (var command in _commands)
                {
                    await Clients.Caller.SendAsync("ReceiveCommand", command);
                }
            }
        }
        finally
        {
            _semaphoreSlim.Release();  
        }

        await base.OnConnectedAsync();
    }

    private void UpdateCachedImage()
    {
        if (_cachedImage == null)
        {
            using (SKImage image = CreateImageFromCommands(_commands))
            {
                if (image != null)
                {
                    using (var encodedImage = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        _cachedImage = encodedImage.ToArray();
                    }
                }
            }
        }
        else
        {
            using (var stream = new MemoryStream(_cachedImage))
            using (var bitmap = SKBitmap.Decode(stream))
            using (var canvas = new SKCanvas(bitmap))
            {
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
                    _cachedImage = encodedImage.ToArray();
                }
            }
        }
    }


    private SKImage CreateImageFromCommands(List<DrawingCommand> commands)
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
using Microsoft.AspNetCore.ResponseCompression;
using SkiaSharp;

var builder = WebApplication.CreateBuilder(args);

byte[] currentImage = null;
Guid lastClientGuid = Guid.Empty;

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.MapPost("/imageUpdate", (ImageData data) =>
{
    currentImage = data.Image;
    lastClientGuid = data.ClientGuid;
    Console.WriteLine($"Received image from client: {lastClientGuid} Length: {currentImage.Length}");
    return "OK";
});

app.MapGet("/lastUser", () =>
{
    Console.WriteLine("received lastuser req");
    return lastClientGuid.ToString();
});

app.MapGet("/getImage", () =>
{
    if (currentImage is null) return Results.NotFound("No image available");

    var stream = new MemoryStream(currentImage);
    return Results.Stream(stream, "image/png");
});


app.Run();

public class ImageData
{
    public byte[] Image { get; set; }
    public Guid ClientGuid { get; set; }
}
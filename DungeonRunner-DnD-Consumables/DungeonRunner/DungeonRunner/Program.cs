using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonRunner.Hubs;
using DungeonRunner.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)  // accept requests from any origin (LAN IPs, localhost)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<StateService>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<TurnEngine>();
builder.Services.AddSingleton<RuleEngine>();

var app = builder.Build();

var state = app.Services.GetRequiredService<StateService>();
state.LoadAll();

app.UseCors();

// Serve uploaded item icons from Data/icons → /icons. The directory is
// created in StateService.LoadAll so it exists before static files mounts.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(state.IconsDir),
    RequestPath = "/icons",
});

app.MapHub<GameHub>("/gamehub");

app.Run();

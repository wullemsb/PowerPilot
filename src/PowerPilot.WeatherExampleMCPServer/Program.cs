using PowerPilot.Core.Interfaces;
using PowerPilot.Infrastructure.Weather;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IWeatherService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>();
    var logger = sp.GetRequiredService<ILogger<OpenWeatherMapService>>();
    return new OpenWeatherMapService(httpClient, options, logger);
});

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Stateless mode is recommended for servers that don't need
        // server-to-client requests like sampling or elicitation.
        // See https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html for details.
        options.Stateless = true;
    })
    .WithTools<WeatherTools>();

var app = builder.Build();
app.MapMcp();
app.UseHttpsRedirection();

app.Run();

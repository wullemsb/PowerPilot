using Microsoft.EntityFrameworkCore;
using PowerPilot.Agents;
using PowerPilot.Core.Interfaces;
using PowerPilot.Infrastructure.Data;
using PowerPilot.Infrastructure.Weather;
using PowerPilot.P1Reader;
using PowerPilot.Web.Components;
using PowerPilot.Web.Hubs;
using PowerPilot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

builder.Services.Configure<P1ReaderOptions>(builder.Configuration.GetSection("P1Reader"));
builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "powerpilot.db");
builder.Services.AddDbContext<EnergyDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Scoped);

builder.Services.AddSingleton<EnergyStateService>();
builder.Services.AddSingleton<IEnergyStateService>(sp => sp.GetRequiredService<EnergyStateService>());
builder.Services.AddScoped<IEnergyRepository, EnergyRepository>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IWeatherService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>();
    var logger = sp.GetRequiredService<ILogger<OpenWeatherMapService>>();
    return new OpenWeatherMapService(httpClient, options, logger);
});

var useSimulated = builder.Configuration.GetValue<bool>("P1Reader:UseSimulated", true);
if (useSimulated)
    builder.Services.AddSingleton<IP1Reader, SimulatedP1Reader>();
else
    builder.Services.AddSingleton<IP1Reader, SerialP1Reader>();

builder.Services.AddSingleton(sp =>
{
    var agentOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value;
    return PowerPilotKernelFactory.CreateKernel(sp, agentOptions.GitHubToken, agentOptions.ModelId);
});
builder.Services.AddScoped<ChatAgentService>();
builder.Services.AddHostedService<P1BackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EnergyDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<EnergyHub>("/energyhub");

app.Run();

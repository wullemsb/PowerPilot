using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using PowerPilot.Agents;
using PowerPilot.Agents.Plugins;
using PowerPilot.Core.Interfaces;
using PowerPilot.Infrastructure.Data;
using PowerPilot.Infrastructure.Weather;
using PowerPilot.P1Reader;
using PowerPilot.Web.Components;
using PowerPilot.Web.Hubs;
using PowerPilot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

// Persist Data Protection keys next to the app instead of relying on the
// user profile (%LOCALAPPDATA%\ASP.NET\DataProtection-Keys). When that
// profile folder isn't writable, key-ring creation throws, the Blazor
// Server circuit's SignalR negotiate call fails, and the whole page freezes.
builder.Services.AddDataProtection()
    .SetApplicationName("PowerPilot")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys")));

builder.Services.Configure<P1ReaderOptions>(builder.Configuration.GetSection("P1Reader"));
builder.Services.Configure<HomeWizardOptions>(builder.Configuration.GetSection("HomeWizard"));
builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<EnergyMonitoringOptions>(builder.Configuration.GetSection("EnergyMonitoring"));

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "powerpilot.db");
builder.Services.AddDbContext<EnergyDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Scoped);

builder.Services.AddSingleton<EnergyStateService>();
builder.Services.AddSingleton<IEnergyStateService>(sp => sp.GetRequiredService<EnergyStateService>());
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IEnergyRepository, EnergyRepository>();
builder.Services.AddScoped<IOnboardingRepository, OnboardingRepository>();
builder.Services.AddScoped<OnboardingService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IWeatherService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>();
    var logger = sp.GetRequiredService<ILogger<OpenWeatherMapService>>();
    return new OpenWeatherMapService(httpClient, options, logger);
});

var meterSource = builder.Configuration.GetValue<string>("MeterSource") ?? "Simulated";
switch (meterSource)
{
    case "HomeWizard": builder.Services.AddSingleton<IP1Reader, HomeWizardP1Reader>(); break;
    case "Serial":     builder.Services.AddSingleton<IP1Reader, SerialP1Reader>();     break;
    default:           builder.Services.AddSingleton<IP1Reader, SimulatedP1Reader>(); break;
}

// GitHub Copilot SDK — one client per application lifetime
builder.Services.AddSingleton(sp =>
{
    var agentOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<GitHub.Copilot.CopilotClient>>();
    return PowerPilotAgentFactory.CreateClient(agentOptions, logger);
});

// Plugins are scoped so they can use the scoped IEnergyRepository
builder.Services.AddScoped<EnergyPlugin>();
builder.Services.AddScoped<WeatherPlugin>();
builder.Services.AddScoped<ChatAgentService>();

// Background services
builder.Services.AddHostedService<P1BackgroundService>();
builder.Services.AddHostedService<EnergyMonitoringAgentService>();

var app = builder.Build();

app.MapDefaultEndpoints();

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

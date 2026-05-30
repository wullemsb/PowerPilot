# PowerPilot Copilot Instructions

## Build, run, and test

- Build the full solution: `dotnet build PowerPilot.slnx`
- Run the full app through Aspire: `dotnet run --project src\PowerPilot.AppHost\PowerPilot.AppHost.csproj`
- Run the web app directly: `dotnet run --project src\PowerPilot.Web\PowerPilot.Web.csproj`
- Run all existing tests: `dotnet test tests\PowerPilot.Tests\PowerPilot.Tests.csproj`
- Run a single test: `dotnet test tests\PowerPilot.Tests\PowerPilot.Tests.csproj --filter "FullyQualifiedName~PowerPilot.Tests.DsmrP1ParserTests.Parse_ValidTelegram_ReturnsCorrectValues"`

## Architecture

- `src\PowerPilot.Web\Program.cs` is the main composition root. It wires the Blazor app, SignalR hub, SQLite `EnergyDbContext`, the selected `IP1Reader` implementation, hosted services, and the Copilot client/plugins.
- `src\PowerPilot.AppHost\AppHost.cs` is the Aspire entry point for local orchestration. Use it when you want the full dashboard-backed development experience.
- `src\PowerPilot.Web\Services\P1BackgroundService.cs` is the live ingestion path: it listens to `IP1Reader`, updates `EnergyStateService`, and persists every sixth reading through `IEnergyRepository`.
- AI chat and monitoring are separate flows. `ChatAgentService` owns the interactive chat session per Blazor circuit, while `EnergyMonitoringAgentService` runs as a background service with its own multi-agent session.

## Configuration conventions

- The app binds configuration sections named `Agent`, `P1Reader`, `Weather`, and `EnergyMonitoring`. Use those exact section names when editing appsettings or user secrets.
- The README still mentions `OpenWeatherMap`, but the code currently binds weather settings from the `Weather` section.
- `P1Reader:UseSimulated` defaults to `true`. Keep it enabled for local development unless you are intentionally connecting a real DSMR meter.
- The SQLite database is created in the web app content root as `powerpilot.db` when the web project starts.
- Copilot authentication can come from `Agent:GitHubToken`; otherwise the app relies on an installed and authenticated Copilot CLI.

## AI integration conventions

- Register or change Copilot tools in `src\PowerPilot.Agents\PowerPilotAgentFactory.cs`. Both the interactive chat path and the monitoring agent build their tool lists from `BuildTools()`.
- Tools created in `BuildTools()` are treated as read-only lookups with `skip_permission = true`. Keep additions there read-only unless you intentionally want a different permission model.
- `ChatAgentService` streams model output token-by-token and reuses a single `CopilotSession` until history is cleared or the service is disposed.
- `EnergyMonitoringAgentService` defines the custom `monitor`, `energy_analyzer`, `appliance_advisor`, and `timing_optimizer` agents inline. Update those prompts and tool scopes there when changing notification behavior.

## Test scope

- Current automated tests live only in `tests\PowerPilot.Tests`.
- Existing coverage is focused on DSMR parsing plus simulated reader / net-power behavior.
- If you change parser or P1 reader logic, extend that test project. There are no existing web or agent integration tests in this repo.

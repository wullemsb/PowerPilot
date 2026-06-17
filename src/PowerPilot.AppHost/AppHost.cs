var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREMCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var mcp = builder.AddProject<Projects.PowerPilot_WeatherExampleMCPServer>("powerpilot-weather-mcp")
    .WithExternalHttpEndpoints()
    .WithMcpServer();
#pragma warning restore ASPIREMCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

builder.AddProject<Projects.PowerPilot_Web>("powerpilot-web")
    .WithReference(mcp)
    .WithExternalHttpEndpoints();



builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PowerPilot_Web>("powerpilot-web")
    .WithExternalHttpEndpoints();

builder.Build().Run();

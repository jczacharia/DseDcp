// Copyright (c) PNC Financial Services. All rights reserved.

using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> api = builder
    .AddProject<Dse_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder
    .AddJavaScriptApp("ui", "../../ui", "start")
    .WithPnpm()
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

await builder.Build().RunAsync();

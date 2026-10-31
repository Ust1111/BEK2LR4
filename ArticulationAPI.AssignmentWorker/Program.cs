//cd ~
//~/.temporalio/bin/temporal server start-dev --db-filename ~/temporal.db
using ArticulationAPI.AssignmentWorker.Activities;
using ArticulationAPI.AssignmentWorker.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTemporalClient(builder.Configuration["Temporal:Address"]!, "default");

builder.Services
    .AddHostedTemporalWorker("assignment-review")
    .AddScopedActivities<AssignmentActivities>()
    .AddWorkflow<ReviewAssignmentWorkflow>();

var host = builder.Build();
host.Services.GetRequiredService<ILogger<Program>>().LogInformation("Worker started, pid {Pid}", Environment.ProcessId);
host.Run();
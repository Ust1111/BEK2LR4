using ArticulationAPI.AssignmentApi.Services;
using Temporalio.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<AssignmentsService>();

builder.Services.AddTemporalClient(builder.Configuration["Temporal:Address"]!, "default");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
using ArticulationAPI.AssignmentApi.Contracts;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace ArticulationAPI.AssignmentApi.Services;

public class AssignmentsService
{
    private const string WorkflowName = "ReviewAssignment";
    private const string TaskQueue = "assignment-review";

    private readonly ITemporalClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssignmentsService> _logger;

    public AssignmentsService(ITemporalClient client, IConfiguration configuration, ILogger<AssignmentsService> logger)
    {
        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    private static string WorkflowIdFor(string assignmentId) => $"assignment-{assignmentId}";

    public async Task<StartAssignmentResponse> StartAsync(StartAssignmentRequest request)
    {
        var assignmentId = Guid.NewGuid().ToString()[..8];

        var handle = await _client.StartWorkflowAsync(
            WorkflowName,
            [assignmentId, request.Student, request.Language, request.Words],
            new WorkflowOptions(id: WorkflowIdFor(assignmentId), taskQueue: TaskQueue));

        _logger.LogInformation("Assignment {AssignmentId} of {Student} accepted, workflow {WorkflowId} started",
            assignmentId, request.Student, handle.Id);

        return new StartAssignmentResponse(
            assignmentId,
            handle.Id,
            $"{_configuration["Temporal:UiUrl"]}/namespaces/default/workflows/{handle.Id}");
    }

    public async Task<AssignmentStatusResponse?> GetStatusAsync(string assignmentId)
    {
        var handle = _client.GetWorkflowHandle(WorkflowIdFor(assignmentId));

        try
        {
            var description = await handle.DescribeAsync();

            if (description.Status != WorkflowExecutionStatus.Running)
            {
                if (description.Status == WorkflowExecutionStatus.Completed)
                {
                    var result = await handle.GetResultAsync<string>();
                    return new AssignmentStatusResponse("completed", Finished: true, Result: result);
                }

                return new AssignmentStatusResponse(description.Status.ToString(), Finished: true);
            }
        }
        catch (RpcException)
        {
            return null;
        }

        try
        {
            var status = await handle.QueryAsync<string>("Status", [], new WorkflowQueryOptions
        {
            Rpc = new RpcOptions { Timeout = TimeSpan.FromSeconds(2) }
        });
        var report = await handle.QueryAsync<string>("Report", [], new WorkflowQueryOptions
        {
            Rpc = new RpcOptions { Timeout = TimeSpan.FromSeconds(2) }
        });

        return new AssignmentStatusResponse(status, WaitingForReview: status.StartsWith("waiting for teacher review"), Report: report);
        }
        catch (Exception)
        {
            return new AssignmentStatusResponse("no worker to answer the query");
        }
    }

    public async Task<bool> ReviewAsync(string assignmentId, ReviewRequest request)
    {
        try
        {
            await _client.GetWorkflowHandle(WorkflowIdFor(assignmentId)).SignalAsync("review", [request.Grade, request.Comment]);
        }
        catch (RpcException)
        {
            return false;
        }

        _logger.LogInformation("Assignment {AssignmentId}: teacher review {Grade} sent", assignmentId, request.Grade);
        return true;
    }
}
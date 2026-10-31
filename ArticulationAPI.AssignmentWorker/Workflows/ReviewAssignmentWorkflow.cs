using ArticulationAPI.AssignmentWorker.Activities;
using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;
using AssignmentActivities;

namespace ArticulationAPI.AssignmentWorker.Workflows;

[Workflow("ReviewAssignment")]
public class ReviewAssignmentWorkflow
{
    private static readonly TimeSpan ReviewTimeout = TimeSpan.FromMinutes(1);
    private const int AutoApproveThreshold = 75;

    private int? _teacherGrade;
    private string? _teacherComment;

    [WorkflowQuery]
    public string Status { get; private set; } = "started";
    
    [WorkflowQuery]
    public string Report { get; private set; } = "";

    [WorkflowSignal("review")]
    public Task ReviewAsync(int grade, string comment)
    {
        _teacherGrade = grade;
        _teacherComment = comment;
        return Task.CompletedTask;
    }

    [WorkflowRun]
    public async Task<string> RunAsync(string assignmentId, string student, string language, WordSubmission[] words)
    {
        var validateOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(10),
            RetryPolicy = new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2, MaximumAttempts = 3 }
        };
        var scoreOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(30),
            HeartbeatTimeout = TimeSpan.FromSeconds(3),
            RetryPolicy = new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2, MaximumAttempts = 5 }
        };
        var feedbackOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(10),
            RetryPolicy = new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2, MaximumAttempts = 3 }
        };
        var publishOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(15),
            RetryPolicy = new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(2), BackoffCoefficient = 2, MaximumAttempts = 5 }
        };
        var notifyOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(10),
            RetryPolicy = new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2, MaximumAttempts = 3 }
        };

        // Этап 1 (последовательный)
        Status = "validating";
        await Workflow.ExecuteActivityAsync(
            (AssignmentActivities a) => a.ValidateSubmissionAsync(assignmentId, student, words.Select(w => w.Word).ToArray()),
            validateOptions);
        // Этап 2 (параллельный): по activity на слово
        Status = $"scoring {words.Length} words";
        var scoreTasks = words
            .Select(submission => Workflow.ExecuteActivityAsync(
                (AssignmentActivities a) => a.ScoreWordAsync(assignmentId, submission, language), scoreOptions))
            .ToArray();

        var scores = await Workflow.WhenAllAsync(scoreTasks);
        var average = (int)Math.Round(scores.Average(s => s.Score));

        // Отчёт для преподавателя: что ввёл студент и что ожидалось
        Report = string.Join("; ", scores.Select(s => $"{s.Word}: студент [{s.Student}], эталон [{s.Reference}], {s.Score}"));

        // Этап 3
        Status = $"preparing feedback (average {average})";
        var feedback = await Workflow.ExecuteActivityAsync(
            (AssignmentActivities a) => a.GenerateFeedbackAsync(assignmentId, student, average), feedbackOptions);

        // Ветвление
        int finalGrade;
        string source;
        if (average >= AutoApproveThreshold)
        {
            finalGrade = average;
            source = "auto";
        }
        else
        {
            var deadline = Workflow.UtcNow + ReviewTimeout;
            Status = $"waiting for teacher review until {deadline:HH:mm:ss} UTC (average {average})";

            // true - пришёл сигнал, false - сработал таймер
            var reviewed = await Workflow.WaitConditionAsync(() => _teacherGrade != null, ReviewTimeout);
            if (reviewed)
            {
                finalGrade = _teacherGrade!.Value;
                source = $"teacher: {_teacherComment}";
            }
            else
            {
                finalGrade = average;
                source = "auto: teacher did not respond in time";
            }
        }

        Status = "publishing";
        await Workflow.ExecuteActivityAsync(
            (AssignmentActivities a) => a.PublishResultAsync(assignmentId, finalGrade, source, feedback), publishOptions);

        Status = "notifying";
        await Workflow.ExecuteActivityAsync(
            (AssignmentActivities a) => a.NotifyStudentAsync(assignmentId, student, finalGrade), notifyOptions);

        Status = "completed";
        return $"grade {finalGrade} ({source}); feedback: {feedback}";
    }
}
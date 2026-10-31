using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace ArticulationAPI.AssignmentWorker.Activities;

public class AssignmentActivities(ILogger<AssignmentActivities> logger)
{
    [Activity]
    public async Task ValidateSubmissionAsync(string assignmentId, string student, string[] words)
    {
        logger.LogInformation("[pid {Pid}] Validating assignment {AssignmentId} from {Student}: {Count} words",
            Environment.ProcessId, assignmentId, student, words.Length);
        await Task.Delay(TimeSpan.FromSeconds(2), ActivityExecutionContext.Current.CancellationToken);
    }

    private static readonly Dictionary<string, string> Reference = new(StringComparer.OrdinalIgnoreCase)
    {
        ["молоко"] = "малако",
        ["собака"] = "сабака",
        ["вода"] = "вада",
        ["город"] = "горат",
        ["хорошо"] = "харашо",
        ["привет"] = "прив'эт",
        ["кот"] = "кот",
        ["мама"] = "мама"
    };

    [Activity]
    public async Task<WordScore> ScoreWordAsync(string assignmentId, WordSubmission submission, string language)
    {
        var context = ActivityExecutionContext.Current;

        logger.LogInformation("[pid {Pid}] Scoring '{Word}' ({Language}) of {AssignmentId}, attempt {Attempt}",
            Environment.ProcessId, submission.Word, language, assignmentId, context.Info.Attempt);

        for (var second = 0; second < 6; second++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);
            context.Heartbeat();
        }

        if (Random.Shared.NextDouble() < 0.3)
        {
            logger.LogWarning("[pid {Pid}] Scoring '{Word}' failed on attempt {Attempt}",
                Environment.ProcessId, submission.Word, context.Info.Attempt);
            throw new InvalidOperationException($"Speech analyzer crashed on '{submission.Word}'");
        }

        if (!Reference.TryGetValue(submission.Word.Trim(), out var reference))
        {
            return new WordScore(submission.Word, submission.Transcription, "(нет эталона)", 0);
        }

        var student = submission.Transcription.Trim().ToLowerInvariant();
        var distance = Levenshtein(student, reference);
        var score = (int)Math.Round(100.0 * (1 - (double)distance / Math.Max(student.Length, reference.Length)));

        logger.LogInformation("[pid {Pid}] '{Word}': student '{Student}', reference '{Reference}', score {Score}",
            Environment.ProcessId, submission.Word, student, reference, score);

        return new WordScore(submission.Word, student, reference, score);
    }

    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    [Activity]
    public async Task<string> GenerateFeedbackAsync(string assignmentId, string student, int averageScore)
    {
        logger.LogInformation("[pid {Pid}] Generating feedback for {Student} ({AssignmentId}), average {Average}",
            Environment.ProcessId, student, assignmentId, averageScore);
        await Task.Delay(TimeSpan.FromSeconds(2), ActivityExecutionContext.Current.CancellationToken);

        return averageScore switch
        {
            >= 75 => "Отличное произношение, так держать",
            >= 55 => "Хорошо, но стоит потренировать сложные слова",
            _ => "Нужно больше практики: повторите задание"
        };
    }

    [Activity]
    public async Task PublishResultAsync(string assignmentId, int finalGrade, string source, string feedback)
    {
        logger.LogInformation("[pid {Pid}] Publishing result of {AssignmentId}: grade {Grade} ({Source}). {Feedback}",
            Environment.ProcessId, assignmentId, finalGrade, source, feedback);
        await Task.Delay(TimeSpan.FromSeconds(3), ActivityExecutionContext.Current.CancellationToken);
    }

    [Activity]
    public async Task NotifyStudentAsync(string assignmentId, string student, int finalGrade)
    {
        logger.LogInformation("[pid {Pid}] Notifying {Student} about {AssignmentId}: grade {Grade}",
            Environment.ProcessId, student, assignmentId, finalGrade);
        await Task.Delay(TimeSpan.FromSeconds(1), ActivityExecutionContext.Current.CancellationToken);
    }
}
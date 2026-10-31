namespace ArticulationAPI.AssignmentApi.Contracts;

public record WordSubmission(string Word, string Transcription);
public record StartAssignmentRequest(string Student, string Language, WordSubmission[] Words);
public record StartAssignmentResponse(string AssignmentId, string WorkflowId, string WorkflowUrl);
public record ReviewRequest(int Grade, string Comment);
public record AssignmentStatusResponse(string Status, bool Finished = false, bool WaitingForReview = false, string? Result = null, string? Report = null);
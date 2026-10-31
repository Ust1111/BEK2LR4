namespace ArticulationAPI.AssignmentWorker.Activities;

public record WordSubmission(string Word, string Transcription);
public record WordScore(string Word, string Student, string Reference, int Score);
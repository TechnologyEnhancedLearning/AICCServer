namespace AICCServer.Models
{
    public record AiccLogEntry(
        string SessionId,
        string RequestType,
        string? CourseId = null,
        string? StudentId = null,
        string? LessonStatus = null,
        string? Score = null,
        string? Time = null,
        string? LessonLocation = null
    );
}

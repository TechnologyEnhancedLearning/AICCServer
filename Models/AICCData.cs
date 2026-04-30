namespace AICCServer.Models
{
    public record AiccDataModel
    {
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string LessonLocation { get; set; } = "";
        public string LessonStatus { get; set; } = "";
        public string Score { get; set; } = "";
        public string Time { get; set; } = "";
        public string CourseId { get; set; } = "";
    }
}

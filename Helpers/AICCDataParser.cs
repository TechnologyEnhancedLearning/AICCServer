using AICCServer.Models;

namespace AICCServer.Helpers
{
    public static class AiccDataParser
    {
        public static AiccDataModel Parse(string rawData)
        {
            var model = new AiccDataModel();
            string currentSection = null;

            var lines = rawData.Split(new[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.Trim());

            foreach (var line in lines)
            {
                var lineClean = line;
                if(lineClean.StartsWith("aicc_data="))
                {
                    lineClean = lineClean.Substring(10);
                }
                // Detect section headers like [Core], [Evaluation], etc.
                if (lineClean.StartsWith("[") && lineClean.EndsWith("]"))
                {
                    currentSection = lineClean.Trim('[', ']')
                                         .ToLower(); // normalize
                    continue;
                }

                // Parse key=value
                var parts = lineClean.Split('=', 2);
                if (parts.Length < 2) continue;

                var key = parts[0].Trim().ToLower();
                var value = parts[1].Trim();

                // Route based on section
                switch (currentSection)
                {
                    case "core":
                        if (key.ToLower() == "student_id") model.StudentId = value;
                        if (key.ToLower() == "student_name") model.StudentName = value;
                        if (key.ToLower() == "lesson_location") model.LessonLocation = value;
                        if (key.ToLower() == "lesson_status") model.LessonStatus = value;
                        if (key.ToLower() == "score") model.Score = value;
                        if (key.ToLower() == "time") model.Time = value;
                        break;

                    case "evaluation":
                        if (key.ToLower() == "course_id") model.CourseId = value;
                        break;

                        // Add more sections if needed
                }
            }
            return model;
        }
    }
}

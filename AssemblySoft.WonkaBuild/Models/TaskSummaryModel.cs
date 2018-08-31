namespace AssemblySoft.WonkaBuild.Models
{
    public class TaskSummaryModel
    {
        public string BuildTasks { get; set; }
        public string BuildLabel { get; set; }
        public string BuildTime { get; set; }
        public string BuildErrors { get; set; }
        public string BuildWarnings { get; set; }
        public string BuildLog { get; set; }
    }
}
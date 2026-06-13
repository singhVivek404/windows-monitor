namespace WorkstationAuditor.Models
{
    public class AnalysisWarning
    {
        public string? Severity { get; set; }
        public string? Message { get; set; }

        public AnalysisWarning() { }
        public AnalysisWarning(string severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }
}

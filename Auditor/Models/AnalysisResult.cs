using System.Collections.Generic;

namespace WorkstationAuditor.Models
{
    public class AnalysisResult
    {
        public int HealthScore { get; set; }
        public List<AnalysisWarning>? Warnings { get; set; }
        public List<string>? Recommendations { get; set; }
    }
}

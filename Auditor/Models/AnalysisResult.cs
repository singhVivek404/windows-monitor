using System.Collections.Generic;

namespace WorkstationAuditor.Models
{
    public class AnalysisResult
    {
        public int              HealthScore     { get; set; }
        public List<AnalysisWarning>? Warnings  { get; set; }
        public List<string>?  Recommendations   { get; set; }

        /// <summary>
        /// Developer-environment-specific findings (WSL2 bloat, cache sizes,
        /// zombie processes, missing tools, etc.) surfaced separately from
        /// generic system warnings.
        /// </summary>
        public List<string>?  DevFindings       { get; set; }
    }
}

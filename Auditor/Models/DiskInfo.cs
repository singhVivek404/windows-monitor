namespace WorkstationAuditor.Models
{
    public class DiskInfo
    {
        public string? Drive { get; set; }
        public double TotalSizeGB { get; set; }
        public double FreeSpaceGB { get; set; }
        public double UsedPercentage { get; set; }
    }
}

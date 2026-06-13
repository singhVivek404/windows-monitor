namespace WorkstationAuditor.Models
{
    public class MachineInfo
    {
        public string? ComputerName { get; set; }
        public string? OSVersion { get; set; }
        public double TotalMemoryGB { get; set; }
        public string? CurrentUser { get; set; }
        public string? BootTime { get; set; }
        public string? CPUName { get; set; }
    }
}

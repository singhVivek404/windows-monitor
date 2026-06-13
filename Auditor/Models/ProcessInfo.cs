namespace WorkstationAuditor.Models
{
    public class ProcessInfo
    {
        public string? Name { get; set; }
        public int Pid { get; set; }
        public double CPU { get; set; }
        public double MemoryMb { get; set; }
    }
}

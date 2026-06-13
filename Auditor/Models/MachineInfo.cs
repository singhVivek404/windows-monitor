namespace WorkstationAuditor.Models
{
    public class MachineInfo
    {
        public string? ComputerName        { get; set; }
        public string? OSVersion           { get; set; }

        /// <summary>Total physical RAM in GB (from Win32_ComputerSystem).</summary>
        public double TotalMemoryGB        { get; set; }

        /// <summary>
        /// Free physical RAM in GB sourced from Win32_OperatingSystem.FreePhysicalMemory.
        /// This is the kernel-accurate figure that accounts for OS, caches, drivers, etc.
        /// </summary>
        public double FreeMemoryGB         { get; set; }

        /// <summary>Used RAM in GB (computed).</summary>
        public double UsedMemoryGB         => System.Math.Max(0, TotalMemoryGB - FreeMemoryGB);

        /// <summary>Used RAM as a percentage of total (computed).</summary>
        public double MemoryUsedPercentage => TotalMemoryGB > 0
            ? System.Math.Round((UsedMemoryGB / TotalMemoryGB) * 100.0, 1) : 0;

        public string? CurrentUser         { get; set; }
        public string? BootTime            { get; set; }
        public string? CPUName             { get; set; }
        public int     CPUCores            { get; set; }
        public int     CPULogicalProcs     { get; set; }
    }
}

using System.Collections.Generic;

namespace WorkstationAuditor.Models
{
    /// <summary>Root model for the devenv.json collector output.</summary>
    public class DevEnvironmentInfo
    {
        public bool                    WslDetected          { get; set; }
        public List<WslDiskInfo>?      WslDisks             { get; set; }

        public bool                    DockerDetected        { get; set; }
        public List<DockerContainer>?  DockerContainers      { get; set; }
        public List<DockerImageInfo>?  DockerImages          { get; set; }

        public List<DevCacheInfo>?     DevCaches             { get; set; }
        public double                  TotalDevCacheSizeGB   { get; set; }

        public List<ZombieProcessInfo>? ZombieProcesses      { get; set; }

        public List<PathToolInfo>?     PathTools             { get; set; }
        public int                     PathLength            { get; set; }
        public bool                    LongPathsEnabled      { get; set; }
    }

    public class WslDiskInfo
    {
        public string? Distribution { get; set; }
        public double  SizeGB       { get; set; }
        public string? VhdxPath     { get; set; }
    }

    public class DockerContainer
    {
        public string? Name   { get; set; }
        public string? Image  { get; set; }
        public string? Status { get; set; }
    }

    public class DockerImageInfo
    {
        public string? Name { get; set; }
        public string? Size { get; set; }
    }

    public class DevCacheInfo
    {
        public string? Name   { get; set; }
        public double  SizeGB { get; set; }
        public string? Path   { get; set; }
    }

    public class ZombieProcessInfo
    {
        public string? ProcessName { get; set; }
        public int     Pid         { get; set; }
        public double  MemoryMb    { get; set; }
        public string? StartTime   { get; set; }
    }

    public class PathToolInfo
    {
        public string? ToolName { get; set; }
        public bool    Found    { get; set; }
        public string? Path     { get; set; }
        public string? Version  { get; set; }
    }
}

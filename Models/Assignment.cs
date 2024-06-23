namespace ai_maestro_proxy.Models
{
    public class Assignment
    {
        public required string Name { get; set; }
        public required string Ip { get; set; }
        public int Port { get; set; }
        public required string GpuIds { get; set; }
        public double AvgGpuWeight { get; set; }
    }
}

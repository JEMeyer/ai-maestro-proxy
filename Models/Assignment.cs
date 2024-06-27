namespace AIMaestroProxy.Models
{
    public class Assignment
    {
        public required string Ip { get; set; }
        public int Port { get; set; }
        public required string GpuIds { get; set; }
    }
}

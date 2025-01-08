namespace AIMaestroProxy.Models
{
    public class ModelAssignment
    {
        public int Port { get; set; }
        public required string Name { get; set; }
        public required string ModelName { get; set; }
        public required string Ip { get; set; }
        public required string GpuIds { get; set; }
    }
}

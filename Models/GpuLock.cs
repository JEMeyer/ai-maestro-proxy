namespace AIMaestroProxy.Models
{
    public class GpuLock
    {
        public required string ModelInUse { get; set; }
        public bool IsLocked { get; set; }
    }
}

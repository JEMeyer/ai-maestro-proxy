namespace ai_maestro_proxy.Models
{
    public class RequestModel
    {
        public required string Model { get; set; }
        public bool? Stream { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}

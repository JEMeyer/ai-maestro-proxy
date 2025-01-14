using System.Text.Json.Serialization;

namespace AIMaestroProxy.Models
{
    public class GpuReservationRequest
    {
        [JsonPropertyName("outputType")]
        public required string OutputType { get; set; }

        [JsonPropertyName("model")]
        public required string ModelName { get; set; }
    }

    public class GpuStatusRequest
    {
        [JsonPropertyName("gpuIds")]
        public required string[] GpuIds { get; set; }
    }
}

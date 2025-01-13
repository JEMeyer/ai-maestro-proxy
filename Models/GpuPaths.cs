
namespace AIMaestroProxy.Models
{
  public static class GpuPaths
  {
    public static readonly string[] ComputeRequired = [
        "api/generate", // ollama
        "api/chat", // ollama
        "api/embeddings", // ollama
        "txt2img", // diffusors-api
        "img2img", // diffuros-api
        "v1/images" // open-ai like (diffusors-api)
        ];
  }
}

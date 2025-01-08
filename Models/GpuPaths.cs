
namespace AIMaestroProxy.Attributes
{
  public static class GpuPaths
  {
    // Using a readonly field instead of const for arrays
    public static readonly string[] ComputeRequired = [
        "api/generate",
        "api/chat",
        "api/embeddings",
        "diffusion/txt2img",
        "diffusion/img2img",
        ];
  }
}

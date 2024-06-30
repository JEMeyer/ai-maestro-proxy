using AIMaestroProxy.Handlers;

namespace AIMaestroProxy.Endpoints
{
    public static class CoquiEndpoints
    {
        public static void MapCoquiEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/clone_speaker", async context =>
            {
                // Stub implementation
                await context.Response.WriteAsync("CloneSpeaker endpoint hit");
            });

            endpoints.MapPost("/tts_stream", async context =>
            {
                // Stub implementation
                await context.Response.WriteAsync("TTSStream endpoint hit");
            });

            endpoints.MapPost("/tts", async context =>
            {
                // Stub implementation
                await context.Response.WriteAsync("TTS endpoint hit");
            });

            endpoints.MapGet("/studio_speakers", async context =>
            {
                // Stub implementation
                await context.Response.WriteAsync("StudioSpeakers endpoint hit");
            });

            endpoints.MapGet("/languages", async context =>
            {
                // Stub implementation
                await context.Response.WriteAsync("Languages endpoint hit");
            });
        }
    }
}

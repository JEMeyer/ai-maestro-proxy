using System.Text.Json;
using AIMaestroProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    [Route("openai/[controller]")]
    public class OpenAIController(ILogger<OpenAIController> logger, HttpClient httpClient, GpuManagerService gpuManagerService) : ControllerBase
    {
        [HttpPost("v1/image/generations")]
        public async Task<IActionResult> ProxyTxt2Img()
        {
            var context = HttpContext;
            try
            {
                // Extract the prompt from the incoming request
                string requestBody;
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                var jsonDoc = JsonDocument.Parse(requestBody);
                if (!jsonDoc.RootElement.TryGetProperty("prompt", out var promptElement))
                {
                    return BadRequest("Missing 'prompt' in the request body.");
                }

                var prompt = promptElement.GetString();

                var responseFormat = "url"; // Default value from OpenAI
                if (jsonDoc.RootElement.TryGetProperty("response_format", out var responseFormatElement))
                {
                    responseFormat = responseFormatElement.GetString() ?? "url";
                }

                // Create the internal request payload
                var internalPayload = new
                {
                    prompt,
                    model = "sdxl-turbo"
                };

                var internalRequestContent = new StringContent(JsonSerializer.Serialize(internalPayload), System.Text.Encoding.UTF8, "application/json");

                var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(internalPayload.model, context.RequestAborted);
                if (modelAssignment == null)
                {
                    return NotFound("No available model assignment found.");
                }

                try
                {
                    // Send the request to the internal /txt2img endpoint
                    var internalResponse = await httpClient.PostAsync($"http://{modelAssignment.Ip}:{modelAssignment.Port}/txt2img", internalRequestContent);

                    if (!internalResponse.IsSuccessStatusCode)
                    {
                        return StatusCode((int)internalResponse.StatusCode, await internalResponse.Content.ReadAsStringAsync());
                    }

                    var internalResponseContent = await internalResponse.Content.ReadAsByteArrayAsync();

                    object responseContent;

                    // Format the response based on response_format
                    if (responseFormat == "b64_json")
                    {
                        responseContent = new
                        {
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            data = new[]
                            {
                            new
                            {
                                b64_json = Convert.ToBase64String(internalResponseContent)
                            }
                        }
                        };
                    }
                    else
                    {
                        responseContent = new
                        {
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            data = new[]
                            {
                            new
                            {
                                url = $"data:image/png;base64,{Convert.ToBase64String(internalResponseContent)}"
                            }
                        }
                        };
                    }

                    return new JsonResult(responseContent);
                }
                finally
                {
                    gpuManagerService.UnlockGPUs(modelAssignment.GpuIds.Split(','));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing /v1/images/generations request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("v1/embeddings")]
        public async Task<IActionResult> ProxyEmbeddings()
        {
            var context = HttpContext;
            try
            {
                // Extract the model and input (prompt) from the incoming request
                string requestBody;
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                var jsonDoc = JsonDocument.Parse(requestBody);
                if (!jsonDoc.RootElement.TryGetProperty("model", out var modelElement))
                {
                    return BadRequest("Missing 'model' in the request body.");
                }

                if (!jsonDoc.RootElement.TryGetProperty("input", out var inputElement))
                {
                    return BadRequest("Missing 'input' in the request body.");
                }

                var model = modelElement.GetString() ?? "mxbai-embed-large";
                var prompt = inputElement.GetString() ?? string.Empty;
                var encodingFormat = "float"; // Default value
                if (jsonDoc.RootElement.TryGetProperty("encoding_format", out var encodingFormatElement))
                {
                    encodingFormat = encodingFormatElement.GetString() ?? "float";
                }

                // Create the internal request payload for Ollama
                var internalPayload = new
                {
                    model,
                    prompt,
                    keep_alive = -1
                };

                var internalRequestContent = new StringContent(JsonSerializer.Serialize(internalPayload), System.Text.Encoding.UTF8, "application/json");

                var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(internalPayload.model, context.RequestAborted);
                if (modelAssignment == null)
                {
                    return NotFound("No available model assignment found.");
                }

                try
                {
                    // Send the request to the Ollama /api/embeddings endpoint
                    var internalResponse = await httpClient.PostAsync($"http://{modelAssignment.Ip}:{modelAssignment.Port}/api/embeddings", internalRequestContent);

                    if (!internalResponse.IsSuccessStatusCode)
                    {
                        return StatusCode((int)internalResponse.StatusCode, await internalResponse.Content.ReadAsStringAsync());
                    }

                    // Read the internal response content
                    var internalResponseContent = await internalResponse.Content.ReadAsStringAsync();
                    var internalResponseJson = JsonDocument.Parse(internalResponseContent);

                    // Extract the embedding array from the internal response
                    if (!internalResponseJson.RootElement.TryGetProperty("embedding", out var embeddingElement))
                    {
                        return StatusCode(500, "Invalid response from internal service: missing 'embedding'.");
                    }

                    var embeddingArray = embeddingElement.EnumerateArray().Select(x => x.GetDouble()).ToArray();

                    object embeddingResult;

                    // Format the response based on encoding_format
                    if (encodingFormat == "base64")
                    {
                        var embeddingBytes = embeddingArray.SelectMany(BitConverter.GetBytes).ToArray();
                        embeddingResult = Convert.ToBase64String(embeddingBytes);
                    }
                    else
                    {
                        embeddingResult = embeddingArray;
                    }

                    // Format the response to match OpenAI's embeddings response structure
                    var responseContent = new
                    {
                        @object = "list",
                        data = new[]
                        {
                        new
                        {
                            @object = "embedding",
                            embedding = embeddingResult,
                            index = 0
                        }
                    },
                        model,
                        usage = new
                        {
                            prompt_tokens = prompt.Split(' ').Length, // Simple token count based on spaces
                            total_tokens = prompt.Split(' ').Length
                        }
                    };

                    return new JsonResult(responseContent);
                }
                finally
                {
                    gpuManagerService.UnlockGPUs(modelAssignment.GpuIds.Split(','));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing /v1/embeddings request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("v1/audio/speech")]
        public async Task<IActionResult> ProxyTTS()
        {
            var context = HttpContext;
            try
            {
                // Extract the model, input, voice, response_format, and speed from the incoming request
                string requestBody;
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                var jsonDoc = JsonDocument.Parse(requestBody);
                if (!jsonDoc.RootElement.TryGetProperty("input", out var inputElement))
                {
                    return BadRequest("Missing 'input' in the request body.");
                }

                var input = inputElement.GetString();

                // Create the internal request payload for the TTS endpoint
                var internalPayload = new
                {
                    speaker_embedding = new List<float>(), // Add appropriate embedding data
                    gpt_cond_latent = new List<List<float>>(), // Add appropriate latent data
                    text = input,
                    language = "en" // Default value, update as needed
                };

                var internalRequestContent = new StringContent(JsonSerializer.Serialize(internalPayload), System.Text.Encoding.UTF8, "application/json");

                var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync("xtts", context.RequestAborted);
                if (modelAssignment == null)
                {
                    return NotFound("No available model assignment found.");
                }

                try
                {
                    // Send the request to the internal /tts endpoint
                    var internalResponse = await httpClient.PostAsync($"http://{modelAssignment.Ip}{modelAssignment.Port}/tts", internalRequestContent);

                    if (!internalResponse.IsSuccessStatusCode)
                    {
                        return StatusCode((int)internalResponse.StatusCode, await internalResponse.Content.ReadAsStringAsync());
                    }

                    // Read the internal response content
                    var internalResponseContent = await internalResponse.Content.ReadAsByteArrayAsync();

                    return File(internalResponseContent, "audio/wav");
                }
                finally
                {
                    gpuManagerService.UnlockGPUs(modelAssignment.GpuIds.Split(','));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing /v1/audio/speech request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("v1/audio/transcriptions")]
        public async Task<IActionResult> ProxyTranscription(IFormFile file, [FromForm] string? language = null, [FromForm] string[]? timestamp_granularities = null)
        {
            var context = HttpContext;
            try
            {
                // Read the file content
                byte[] fileContent;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }

                // Create the internal request payload for transcription
                var internalPayload = new
                {
                    model = "openai/whisper-large-v3",
                    task = "transcribe",
                    language,
                    timestamp = timestamp_granularities?.Length > 0 ? "word" : "segment"
                };

                var tempFilePath = Path.GetTempFileName();
                await System.IO.File.WriteAllBytesAsync(tempFilePath, fileContent);

                // Create the MultipartFormDataContent
                var form = new MultipartFormDataContent
                {
                    { new StringContent(JsonSerializer.Serialize(internalPayload)), "payload" },
                    { new StreamContent(new MemoryStream(fileContent)), "file", file.FileName }
                };

                var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(internalPayload.model, context.RequestAborted);
                if (modelAssignment == null)
                {
                    return NotFound("No available model assignment found.");
                }

                try
                {

                    // Send the request to the internal /transcribe endpoint
                    var internalResponse = await httpClient.PostAsync("http://your-internal-service/transcribe", form);

                    if (!internalResponse.IsSuccessStatusCode)
                    {
                        return StatusCode((int)internalResponse.StatusCode, await internalResponse.Content.ReadAsStringAsync());
                    }

                    // Read the internal response content
                    var internalResponseContent = await internalResponse.Content.ReadAsStringAsync();
                    var responseJson = JsonDocument.Parse(internalResponseContent);

                    // Format the response based on response_format
                    object responseContent = responseJson == null ? new { text = string.Empty } : new { text = responseJson.RootElement.GetProperty("text").GetString() ?? string.Empty };

                    return new JsonResult(responseContent);
                }
                finally
                {
                    gpuManagerService.UnlockGPUs(modelAssignment.GpuIds.Split(','));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing /v1/audio/transcriptions request");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Text;
using ai_maestro_proxy.Models;
using ai_maestro_proxy.Services;
using Serilog;
using Newtonsoft.Json;

namespace ai_maestro_proxy.Controllers
{
    [ApiController]
    [Route("")]
    public class ProxyController(DatabaseService databaseService, CacheService cacheService, IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private static readonly SemaphoreSlim semaphore = new(initialCount: 1, maxCount: 1);
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<(HttpContext context, string body, bool shouldCheckStream)>> queues = new();
        private static readonly ConcurrentDictionary<string, HashSet<string>> lockedGpus = new();
        private readonly DatabaseService databaseService = databaseService;
        private readonly CacheService cacheService = cacheService;
        private readonly IHttpClientFactory httpClientFactory = httpClientFactory;

        [HttpPost("txt2img")]
        public async Task<IActionResult> HandleTxt2Img([FromBody] RequestModel request)
        {
            Log.Information("Endpoint hit: {Endpoint}, Model requested: {Model}", "txt2img", request.Model);
            return await HandleRequest(endpoint: "txt2img", request: request, shouldCheckStream: false, originalRequestBody: await ReadRequestBodyAsync());
        }

        [HttpPost("img2img")]
        public async Task<IActionResult> HandleImgImg([FromBody] RequestModel request)
        {
            return await HandleRequest(endpoint: "img2img", request: request, shouldCheckStream: false, originalRequestBody: await ReadRequestBodyAsync());
        }

        [HttpPost("api/generate")]
        public async Task<IActionResult> Handlegenerate([FromBody] RequestModel request)
        {
            return await HandleRequest(endpoint: "api/generate", request: request, shouldCheckStream: true, originalRequestBody: await ReadRequestBodyAsync());
        }

        [HttpPost("api/chat")]
        public async Task<IActionResult> HandleChat([FromBody] RequestModel request)
        {
            return await HandleRequest(endpoint: "api/chat", request: request, shouldCheckStream: true, originalRequestBody: await ReadRequestBodyAsync());
        }

        [HttpPost("api/embeddings")]
        public async Task<IActionResult> HandleEmbeddings([FromBody] RequestModel request)
        {
            return await HandleRequest(endpoint: "api/embeddings", request: request, shouldCheckStream: true, originalRequestBody: await ReadRequestBodyAsync());
        }

        private async Task<IActionResult> HandleRequest(string endpoint, RequestModel request, bool shouldCheckStream, string originalRequestBody)
        {
            Log.Information("Handling request for endpoint: {Endpoint}, Model: {Model}", endpoint, request.Model);

            string? cacheKey = $"model:{request.Model}";
            string? cachedAssignments = await cacheService.GetCachedValueAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedAssignments))
            {
                Log.Information("Cache hit for model: {Model}", request.Model);
            }
            else
            {
                Log.Information("Cache miss for model: {Model}", request.Model);
            }

            IEnumerable<Assignment>? assignmentList;
            if (!string.IsNullOrEmpty(cachedAssignments))
            {
                assignmentList = JsonConvert.DeserializeObject<IEnumerable<Assignment>>(cachedAssignments);
            }
            else
            {
                try
                {
                    Log.Information("Fetching assignments from database for model: {Model}", request.Model);
                    assignmentList = await databaseService.GetAssignmentsAsync(request.Model);
                    await cacheService.SetCachedValueAsync(cacheKey, JsonConvert.SerializeObject(assignmentList), TimeSpan.FromMinutes(5));
                    Log.Information("Fetched and cached assignments for model: {Model}", request.Model);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error fetching assignments from database for model: {Model}", request.Model);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error fetching assignments");
                }
            }

            if (assignmentList == null || !assignmentList.Any())
            {
                Log.Information("No assignments found for model: {Model}", request.Model);
                return StatusCode(StatusCodes.Status500InternalServerError, "No assignments found");
            }

            Assignment? selectedAssignment = null;
            await semaphore.WaitAsync();
            try
            {
                foreach (Assignment assignment in assignmentList)
                {
                    string[] gpuIds = assignment.GpuIds.Split(',');
                    if (AreGpusAvailable(gpuIds))
                    {
                        LockGpus(gpuIds);
                        selectedAssignment = assignment;
                        break;
                    }
                }

                if (selectedAssignment == null)
                {
                    Log.Information("All GPUs are taken, adding request to queue for model: {Model}", request.Model);
                    if (!queues.TryGetValue(request.Model, out ConcurrentQueue<(HttpContext context, string body, bool shouldCheckStream)>? value))
                    {
                        value = new ConcurrentQueue<(HttpContext, string, bool)>();
                        queues[request.Model] = value;
                    }

                    value.Enqueue((HttpContext, originalRequestBody, shouldCheckStream));
                    return new StatusCodeResult(StatusCodes.Status202Accepted);
                }
            }
            finally
            {
                semaphore.Release();
            }

            Uri? proxyUri = new Uri($"http://{selectedAssignment.Ip}:{selectedAssignment.Port}/{endpoint}");
            HttpClient? client = httpClientFactory.CreateClient();

            // Use the original request body
            string? proxiedRequestBody = originalRequestBody;
            Log.Information("Original request body: {RequestBody}", proxiedRequestBody);
            if (string.IsNullOrEmpty(proxiedRequestBody))
            {
                return BadRequest("Request body is empty or invalid.");
            }

            JObject? jsonBody = JObject.Parse(proxiedRequestBody);
            jsonBody["model"] = request.Model;
            if (shouldCheckStream)
            {
                jsonBody["stream"] = request.Stream ?? true;
            }
            string? updatedRequestBody = jsonBody.ToString();
            Log.Information("Proxied request body: {RequestBody}", updatedRequestBody);

            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = new HttpMethod(HttpContext.Request.Method),
                RequestUri = proxyUri,
                Content = new StringContent(updatedRequestBody, Encoding.UTF8, "application/json")
            };

            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in HttpContext.Request.Headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            HttpResponseMessage? responseMessage = null;
            try
            {
                Log.Information("Sending proxied request to {ProxyUri}", proxyUri);
                responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                HttpContext.Response.StatusCode = (int)responseMessage.StatusCode;
                foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
                {
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }

                foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Content.Headers)
                {
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }

                HttpContext.Response.Headers.Remove("transfer-encoding");
                await responseMessage.Content.CopyToAsync(HttpContext.Response.Body);
            }
            catch (HttpRequestException ex)
            {
                System.Net.HttpStatusCode statusCode = responseMessage?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError;
                string responseBody = responseMessage != null ? await responseMessage.Content.ReadAsStringAsync() : "No response body";
                Log.Error(ex, "HTTP request to {ProxyUri} failed with status code {StatusCode}. Response body: {ResponseBody}", proxyUri, statusCode, responseBody);
                return StatusCode((int)statusCode, responseBody);
            }
            finally
            {
                stopwatch.Stop();
                Log.Information("Request completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                await ReleaseGpus(selectedAssignment.GpuIds.Split(','));
            }

            return Ok();
        }

        private static bool AreGpusAvailable(string[] gpuIds)
        {
            foreach (string gpuId in gpuIds)
            {
                if (lockedGpus.Values.Any(gpus => gpus.Contains(gpuId)))
                {
                    return false;
                }
            }
            return true;
        }

        private static void LockGpus(string[] gpuIds)
        {
            foreach (string gpuId in gpuIds)
            {
                lockedGpus.GetOrAdd(gpuId, _ => new HashSet<string>()).Add(gpuId);
                Log.Information("GPU {GpuId} marked as taken", gpuId);
            }
        }

        private async Task ReleaseGpus(string[] gpuIds)
        {
            foreach (string gpuId in gpuIds)
            {
                if (lockedGpus.TryGetValue(gpuId, out HashSet<string>? value))
                {
                    value.Remove(gpuId);
                    if (value.Count == 0)
                    {
                        lockedGpus.TryRemove(gpuId, out _);
                    }
                    Log.Information("GPU {GpuId} marked as available", gpuId);
                }
            }

            foreach (KeyValuePair<string, ConcurrentQueue<(HttpContext context, string body, bool shouldCheckStream)>> queue in queues)
            {
                if (queue.Value.TryDequeue(out (HttpContext context, string body, bool shouldCheckStream) context))
                {
                    string model = queue.Key;
                    if (!queues[model].IsEmpty)
                    {
                        Log.Information("Releasing request from queue for model {Model}", model);
                        await HandleRequest("[queued proxy]", JsonConvert.DeserializeObject<RequestModel>(context.body)!, context.shouldCheckStream, context.body);
                    }
                }
            }
        }

        private async Task<string> ReadRequestBodyAsync()
        {
            HttpContext.Request.EnableBuffering();
            using StreamReader reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            string body = await reader.ReadToEndAsync();
            HttpContext.Request.Body.Position = 0;
            return body;
        }
    }

    public class RequestModel
    {
        public required string Model { get; set; }
        public bool? Stream { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}

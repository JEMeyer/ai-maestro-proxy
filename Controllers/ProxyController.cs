using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ai_maestro_proxy.Models;
using ai_maestro_proxy.Services;
using ai_maestro_proxy.Utilities;
using Serilog;

namespace ai_maestro_proxy.Controllers
{
    [ApiController]
    [Route("")]
    public class ProxyController(DatabaseService databaseService, CacheService cacheService, IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<(HttpContext context, string body)>> queues = new();
        private static readonly ConcurrentDictionary<string, HashSet<string>> lockedGpus = new();

        [HttpPost("txt2img")]
        public async Task<IActionResult> HandleTxt2Img([FromBody] RequestModel request)
        {
            Log.Information("Endpoint hit: {Endpoint}, Model requested: {Model}", "txt2img", request.Model);
            return await HandleRequest("txt2img", request);
        }

        [HttpPost("img2img")]
        public async Task<IActionResult> HandleImgImg([FromBody] RequestModel request)
        {
            return await HandleRequest("txt2img", request);
        }

        [HttpPost("api/generate")]
        public async Task<IActionResult> Handlegenerate([FromBody] RequestModel request)
        {
            return await HandleRequest("api/generate", request);
        }

        [HttpPost("api/chat")]
        public async Task<IActionResult> HandleChat([FromBody] RequestModel request)
        {
            return await HandleRequest("api/chat", request);
        }

        [HttpPost("api/embeddings")]
        public async Task<IActionResult> HandleEmbeddings([FromBody] RequestModel request)
        {
            return await HandleRequest("api/embeddings", request);
        }

        private async Task<IActionResult> HandleRequest(string endpoint, RequestModel request)
        {
            Log.Information("Handling request for endpoint: {Endpoint}, Model: {Model}", endpoint, request.Model);

            var cacheKey = $"model:{request.Model}";
            var cachedAssignments = await cacheService.GetCachedValueAsync(cacheKey);

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
                foreach (var assignment in assignmentList)
                {
                    var gpuIds = assignment.GpuIds.Split(',');
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
                    var body = await HttpContext.ReadRequestBodyAsync();
                    if (!queues.TryGetValue(request.Model, out ConcurrentQueue<(HttpContext context, string body)>? value))
                    {
                        value = new ConcurrentQueue<(HttpContext, string)>();
                        queues[request.Model] = value;
                    }

                    value.Enqueue((HttpContext, body));
                    return new StatusCodeResult(StatusCodes.Status202Accepted);
                }
            }
            finally
            {
                semaphore.Release();
            }

            var proxyUri = new Uri($"http://{selectedAssignment.Ip}:{selectedAssignment.Port}/{endpoint}");
            var client = httpClientFactory.CreateClient();

            // Convert RequestModel to Dictionary
            var requestDict = new Dictionary<string, object>(request.AdditionalData)
            {
                { "model", request.Model },
                { "stream", request.Stream ?? true }
            };

            // Serialize the dictionary to JSON
            var requestBody = JsonConvert.SerializeObject(requestDict);
            Log.Information("Proxied request body: {RequestBody}", requestBody);

            var requestMessage = new HttpRequestMessage
            {
                Method = new HttpMethod(HttpContext.Request.Method),
                RequestUri = proxyUri,
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            foreach (var header in HttpContext.Request.Headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            HttpResponseMessage? responseMessage = null;
            try
            {
                Log.Information("Sending proxied request to {ProxyUri}", proxyUri);
                responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                HttpContext.Response.StatusCode = (int)responseMessage.StatusCode;
                foreach (var header in responseMessage.Headers)
                {
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }

                foreach (var header in responseMessage.Content.Headers)
                {
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }

                HttpContext.Response.Headers.Remove("transfer-encoding");
                await responseMessage.Content.CopyToAsync(HttpContext.Response.Body);
            }
            catch (HttpRequestException ex)
            {
                var statusCode = responseMessage?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError;
                var responseBody = responseMessage != null ? await responseMessage.Content.ReadAsStringAsync() : "No response body";
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
            foreach (var gpuId in gpuIds)
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
            foreach (var gpuId in gpuIds)
            {
                lockedGpus.GetOrAdd(gpuId, _ => []).Add(gpuId);
                Log.Information("GPU {GpuId} marked as taken", gpuId);
            }
        }

        private async Task ReleaseGpus(string[] gpuIds)
        {
            foreach (var gpuId in gpuIds)
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

            foreach (var queue in queues)
            {
                if (queue.Value.TryDequeue(out var contextPair))
                {
                    var model = queue.Key;
                    if (!queues[model].IsEmpty)
                    {
                        Log.Information("Releasing request from queue for model {Model}", model);
                        contextPair.context.WriteRequestBody(contextPair.body);
                        await HandleRequest("proxy", JsonConvert.DeserializeObject<RequestModel>(contextPair.body)!);
                    }
                }
            }
        }
    }

    public class RequestModel
    {
        public required string Model { get; set; }
        public bool? Stream { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = [];
    }
}

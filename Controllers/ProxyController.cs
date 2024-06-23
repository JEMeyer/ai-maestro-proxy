using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<(HttpContext context, string body)>> _queues = new();
        private static readonly ConcurrentDictionary<string, HashSet<string>> _lockedGpus = new();

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

        public async Task<IActionResult> HandleRequest(string endpoint, [FromBody] RequestModel request)
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
            await _semaphore.WaitAsync();
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
                    if (!_queues.ContainsKey(request.Model))
                    {
                        _queues[request.Model] = new ConcurrentQueue<(HttpContext, string)>();
                    }
                    _queues[request.Model].Enqueue((HttpContext, body));
                    return new StatusCodeResult(StatusCodes.Status202Accepted);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            var proxyUri = new Uri($"http://{selectedAssignment.Ip}:{selectedAssignment.Port}/{endpoint}");
            var client = httpClientFactory.CreateClient();

            var requestMessage = new HttpRequestMessage
            {
                Method = new HttpMethod(HttpContext.Request.Method),
                RequestUri = proxyUri,
                Content = new StringContent(await HttpContext.ReadRequestBodyAsync(), System.Text.Encoding.UTF8, "application/json")
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
                if (_lockedGpus.Values.Any(gpus => gpus.Contains(gpuId)))
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
                _lockedGpus.GetOrAdd(gpuId, _ => new HashSet<string>()).Add(gpuId);
                Log.Information("GPU {GpuId} marked as taken", gpuId);
            }
        }

        private async Task ReleaseGpus(string[] gpuIds)
        {
            foreach (var gpuId in gpuIds)
            {
                if (_lockedGpus.TryGetValue(gpuId, out HashSet<string>? value))
                {
                    value.Remove(gpuId);
                    if (value.Count == 0)
                    {
                        _lockedGpus.TryRemove(gpuId, out _);
                    }
                    Log.Information("GPU {GpuId} marked as available", gpuId);
                }
            }

            foreach (var queue in _queues)
            {
                if (queue.Value.TryDequeue(out var contextPair))
                {
                    var model = queue.Key;
                    if (!_queues[model].IsEmpty)
                    {
                        Log.Information("Releasing request from queue for model {Model}", model);
                        contextPair.context.WriteRequestBody(contextPair.body);
                        RequestModel? deserializedRequest = JsonConvert.DeserializeObject<RequestModel>(contextPair.body);
                        if (deserializedRequest != null)
                        {
                            await HandleRequest("proxy", deserializedRequest);
                        }
                    }
                }
            }
        }
    }

    public class RequestModel
    {
        public required string Model { get; set; }
        public bool? Stream { get; set; }
    }
}

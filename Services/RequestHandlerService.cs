using ai_maestro_proxy.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Collections.Concurrent;

namespace ai_maestro_proxy.Services
{
    public class RequestHandlerService(DatabaseService databaseService, CacheService cacheService, GpuManagerService gpuManagerService, ProxiedRequestService proxiedRequestService)
    {
        private static readonly SemaphoreSlim semaphore = new(1, 1);
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<RequestQueueItem>> queues = new();

        public async Task<IActionResult> HandleRequest(string endpoint, RequestModel request, bool shouldCheckStream, string originalRequestBody, HttpContext httpContext, CancellationToken cancellationToken)
        {
            Log.Information("Handling request for endpoint: {Endpoint}, Model: {Model}", endpoint, request.Model);
            string cacheKey = $"model:{request.Model}";

            IEnumerable<Assignment> assignmentList;
            try
            {
                assignmentList = await FetchAssignmentsAsync(request.Model, cacheKey);
            }
            catch
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            if (!assignmentList.Any())
            {
                Log.Information("No assignments found for model: {Model}", request.Model);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            using (GpuLock? gpuLock = TryAcquireGpus(assignmentList))
            {
                if (gpuLock == null)
                {
                    Log.Information("All GPUs are taken, adding request to queue for model: {Model}", request.Model);
                    return await QueueRequest(request.Model, httpContext, originalRequestBody, shouldCheckStream, cancellationToken);
                }

                Uri proxyUri = new($"http://{gpuLock.Assignment.Ip}:{gpuLock.Assignment.Port}/{endpoint}");
                if (string.IsNullOrEmpty(originalRequestBody))
                {
                    return new BadRequestObjectResult("Request body is empty or invalid.");
                }

                JObject jsonBody = JObject.Parse(originalRequestBody);
                jsonBody["model"] = request.Model;
                if (shouldCheckStream)
                {
                    jsonBody["stream"] = request.Stream ?? true;
                }
                foreach (var kvp in request.AdditionalData)
                {
                    jsonBody[kvp.Key] = JToken.FromObject(kvp.Value);
                }
                string updatedRequestBody = jsonBody.ToString();

                var requestMessage = proxiedRequestService.CreateProxiedRequestMessage(updatedRequestBody, proxyUri, httpContext);
                await proxiedRequestService.SendProxiedRequest(requestMessage, proxyUri, httpContext, cancellationToken);
            }

            // The GPU will be automatically unlocked when the GpuLock object is disposed at the end of the using block.
            return new EmptyResult();
        }

        private async Task<IEnumerable<Assignment>> FetchAssignmentsAsync(string model, string cacheKey)
        {
            string? cachedAssignmentsString = await cacheService.GetCachedValueAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedAssignmentsString))
            {
                Log.Information("Cache hit for model: {Model}", model);
                var cachedAssignments = JsonConvert.DeserializeObject<IEnumerable<Assignment>>(cachedAssignmentsString);
                if (cachedAssignments == null)
                {
                    Log.Warning("Cache for model {Model} did not deserialize. Clearing cache and acting as a miss.", model);
                    await cacheService.ClearCachedValueAsync(cacheKey);
                }
                else
                {
                    return cachedAssignments;
                }
            }

            Log.Information("Cache miss for model: {Model}", model);

            try
            {
                Log.Information("Fetching assignments from database for model: {Model}", model);
                var assignments = await databaseService.GetAssignmentsAsync(model);
                await cacheService.SetCachedValueAsync(cacheKey, JsonConvert.SerializeObject(assignments), TimeSpan.FromDays(1));
                Log.Information("Fetched and cached assignments for model: {Model}", model);
                return assignments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching assignments from database for model: {Model}", model);
                throw;
            }
        }

        private GpuLock? TryAcquireGpus(IEnumerable<Assignment> assignments)
        {
            foreach (var assignment in assignments)
            {
                string[] gpuIds = assignment.GpuIds.Split(',');
                if (gpuManagerService.TryLockGpus(gpuIds))
                {
                    return new GpuLock(gpuManagerService, assignment);
                }
            }
            return null;
        }


        private static async Task<IActionResult> QueueRequest(string model, HttpContext context, string requestBody, bool shouldCheckStream, CancellationToken cancellationToken)
        {
            var queueItem = new RequestQueueItem(context, requestBody, shouldCheckStream, cancellationToken);
            queues.GetOrAdd(model, _ => new ConcurrentQueue<RequestQueueItem>()).Enqueue(queueItem);
            return await queueItem.CompletionSource.Task;
        }

        private void ProcessQueue()
        {
            foreach (var kvp in queues)
            {
                if (kvp.Value.TryDequeue(out var queueItem))
                {
                    HandleRequestFromQueue(queueItem).ContinueWith(task =>
                    {
                        if (task.IsFaulted && task.Exception != null)
                        {
                            queueItem.CompletionSource.SetException(task.Exception);
                        }
                        else if (task.IsCanceled)
                        {
                            queueItem.CompletionSource.SetCanceled();
                        }
                        else
                        {
                            queueItem.CompletionSource.SetResult(task.Result);
                        }
                    });
                }
            }
        }

        private async Task<IActionResult> HandleRequestFromQueue(RequestQueueItem queueItem)
        {
            RequestModel? deserializedBody = JsonConvert.DeserializeObject<RequestModel>(queueItem.Body);
            ArgumentNullException.ThrowIfNull(deserializedBody);
            return await HandleRequest(
                queueItem.Context.Request.Path,
                deserializedBody,
                queueItem.ShouldCheckStream,
                queueItem.Body,
                queueItem.Context,
                queueItem.CancellationToken
            );
        }
    }
}

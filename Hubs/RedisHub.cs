using Microsoft.AspNetCore.SignalR;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Hubs
{
    public class RedisHub(RedisSubscriberService redisSubscriberService) : Hub
    {
        public Task SubscribeToChannel(string channelName)
        {
            return redisSubscriberService.SubscribeToChannel(channelName);
        }

        public Task PublishToChannel(string channelName, string message)
        {
            return redisSubscriberService.PublishToChannel(channelName, message);
        }

        public Task<Dictionary<string, string>> GetKeyValuePairsByPattern(string pattern)
        {
            return redisSubscriberService.GetKeyValuePairsByPattern(pattern);
        }
    }
}

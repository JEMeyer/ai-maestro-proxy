using AIMaestroProxy.Hubs;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class RedisSubscriberService(IConnectionMultiplexer redis, IHubContext<RedisHub> hubContext, ILogger<RedisSubscriberService> _logger)
    {
        public async Task SubscribeToChannel(string channelName)
        {
            var subscriber = redis.GetSubscriber();
            var redisChannel = new RedisChannel(channelName, RedisChannel.PatternMode.Literal);
            await subscriber.SubscribeAsync(redisChannel, async (channel, message) =>
            {
                var messageContent = message.ToString(); // Ensure the message is a string
                _logger.LogDebug("Received message: {messageContent} on channel: {channel}", messageContent, channel);

                // Send the message to clients
                await hubContext.Clients.All.SendAsync("ReceiveMessage", channel.ToString(), messageContent);
            });
        }

        public async Task PublishToChannel(string channelName, string message)
        {
            var subscriber = redis.GetSubscriber();
            var redisChannel = new RedisChannel(channelName, RedisChannel.PatternMode.Literal);
            _logger.LogDebug("Publishing message: {message} on channel: {redisChannel}", message, redisChannel);
            await subscriber.PublishAsync(redisChannel, message);
        }

        public async Task<Dictionary<string, string>> GetKeyValuePairsByPattern(string pattern)
        {
            var db = redis.GetDatabase();
            var server = redis.GetServer(redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            var result = new Dictionary<string, string>();

            foreach (var key in keys)
            {
                var value = await db.StringGetAsync(key);
                result[key.ToString()] = value.HasValue ? value.ToString() : string.Empty;
            }

            _logger.LogDebug("Received {resultCount} key-value pairs for pattern: {pattern}", result.Count, pattern);
            return result;
        }
    }
}

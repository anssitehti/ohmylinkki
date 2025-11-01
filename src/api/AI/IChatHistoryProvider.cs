using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.AI;

public interface IChatHistoryProvider
{
    AgentThread LoadConversation(string userId, AIAgent agent);
    public void SaveConversation(string userId, AgentThread thread);

    void ClearHistory(string userId);
}

public class MemoryChatHistoryProvider(
    IMemoryCache memoryCache,
    IOptions<OpenAiOptions> options) : IChatHistoryProvider
{
    public AgentThread LoadConversation(string userId, AIAgent agent)
    {
        var serializedJson = memoryCache.Get<string>(CreateCacheKey(userId));
        if (string.IsNullOrWhiteSpace(serializedJson))
        {
            return agent.GetNewThread();
        }

        var reloaded = JsonSerializer.Deserialize<JsonElement>(serializedJson, JsonSerializerOptions.Web);
        return agent.DeserializeThread(reloaded, JsonSerializerOptions.Web);
    }

    public void SaveConversation(string userId, AgentThread thread)
    {
        var serializedJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        memoryCache.Set(CreateCacheKey(userId), serializedJson,
            TimeSpan.FromMinutes(options.Value.ChatHistoryExpirationMinutes));
    }

    public void ClearHistory(string userId)
    {
        memoryCache.Remove(CreateCacheKey(userId));
    }

    private string CreateCacheKey(string userId)
    {
        return $"{userId}_agent";
    }
}
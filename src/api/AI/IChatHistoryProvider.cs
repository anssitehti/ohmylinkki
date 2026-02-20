using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.AI;

public interface IChatHistoryProvider
{
    Task<AgentSession> LoadConversationAsync(string userId, AIAgent agent);
    public Task SaveConversationAsync(string userId, AIAgent agent, AgentSession agentSession);

    void ClearHistory(string userId);
}

public class MemoryChatHistoryProvider(
    IMemoryCache memoryCache,
    IOptions<OpenAiOptions> options) : IChatHistoryProvider
{
    public async Task<AgentSession> LoadConversationAsync(string userId, AIAgent agent)
    {
        var serializedJson = memoryCache.Get<string>(CreateCacheKey(userId));
        if (string.IsNullOrWhiteSpace(serializedJson))
        {
            return await agent.CreateSessionAsync();
        }

        var reloaded = JsonSerializer.Deserialize<JsonElement>(serializedJson, JsonSerializerOptions.Web);
        return await agent.DeserializeSessionAsync(reloaded, JsonSerializerOptions.Web);
    }

    public async Task SaveConversationAsync(string userId, AIAgent agent, AgentSession agentSession)
    {
        var serializedJson = await agent.SerializeSessionAsync(agentSession, JsonSerializerOptions.Web);
        memoryCache.Set(CreateCacheKey(userId), serializedJson.GetRawText(),
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
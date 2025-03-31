using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Api.AI;

public interface IChatHistoryProvider
{
    Task<ChatHistory> GetHistoryAsync(string userId);

    Task<ChatHistory> GetAgentHistoryAsync(string userId);

    Task ClearHistoryAsync(string userId);
}

public class MemoryChatHistoryProvider(
    IMemoryCache memoryCache,
    IOptions<OpenAiOptions> options,
    IChatHistoryReducer historyReducer) : IChatHistoryProvider
{
    public async Task<ChatHistory> GetHistoryAsync(string userId)
    {
        return await GetChatHistoryAsync(userId, "chat", true);
    }

    public async Task<ChatHistory> GetAgentHistoryAsync(string userId)
    {
        return await GetChatHistoryAsync(userId, "agent", false);
    }

    private async Task<ChatHistory> GetChatHistoryAsync(string userId, string type, bool includeInstructions)
    {
        var cacheKey = $"{userId}_{type}";

        var chatHistory = await memoryCache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.ChatHistoryExpirationMinutes);
            return Task.FromResult(includeInstructions
                ? new ChatHistory(AgentInstructions.LinkkiAgentInstructions)
                : new ChatHistory());
        });

        var reducedMessages = await historyReducer.ReduceAsync(chatHistory!);

        if (reducedMessages == null) return chatHistory!;

        var reducedHistory = new ChatHistory(reducedMessages);
        memoryCache.Set(cacheKey, reducedHistory,
            TimeSpan.FromMinutes(options.Value.ChatHistoryExpirationMinutes));
        return reducedHistory;
    }

    public Task ClearHistoryAsync(string userId)
    {
        memoryCache.Remove($"{userId}_chat");
        memoryCache.Remove($"{userId}_agent");
        return Task.CompletedTask;
    }
}
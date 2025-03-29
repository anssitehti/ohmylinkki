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

public class MemoryChatHistoryProvider(IMemoryCache memoryCache, IOptions<OpenAiOptions> options) : IChatHistoryProvider
{

    public async Task<ChatHistory> GetHistoryAsync(string userId)
    {
        var chatHistory = await memoryCache.GetOrCreateAsync(userId, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.ChatHistoryExpirationMinutes);
            return Task.FromResult(new ChatHistory(AgentInstructions.LinkkiAgentInstructions));
        });

        return chatHistory!;
    }

    public async Task<ChatHistory> GetAgentHistoryAsync(string userId)
    {
        var chatHistory = await memoryCache.GetOrCreateAsync(userId, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.ChatHistoryExpirationMinutes);
            return Task.FromResult(new ChatHistory());
        });

        return chatHistory!;
    }

    public Task ClearHistoryAsync(string userId)
    {
        memoryCache.Remove(userId);
        return Task.CompletedTask;
    }
}
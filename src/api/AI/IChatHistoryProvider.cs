using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Api.AI;

public interface IChatHistoryProvider
{
    Task<ChatHistory> GetHistoryAsync(string userId);
}

public class MemoryChatHistoryProvider(IMemoryCache memoryCache, IOptions<OpenAiOptions> options) : IChatHistoryProvider
{
    private const string SystemMessage =
        "You are a helpful assistant that helps users with Linkki bus lines in Jyväskylä.";


    public async Task<ChatHistory> GetHistoryAsync(string userId)
    {
        var chatHistory = await memoryCache.GetOrCreateAsync(userId, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.ChatHistoryExpirationMinutes);
            return Task.FromResult(new ChatHistory(SystemMessage));
        });

        return chatHistory!;
    }
}
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
    private const string SystemMessage = """
                                         You are a friendly and knowledgeable assistant dedicated to helping users with Linkki bus lines in Jyväskylä. 
                                         
                                         You can:
                                         
                                         Answer questions about bus lines and locations.
                                         Provide real-time information about the current location of buses.
                                        
                                         All your responses should be in Markdown format.
                                         """;

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
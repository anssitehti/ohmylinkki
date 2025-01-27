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
                                         
                                         Here are some guidelines for your responses:
                                         
                                         Answer questions about bus lines and locations.
                                         Provide real-time information about the current location of buses.
                                         All your responses should be in Markdown format.
                                         Keep answers short and to the point.
                                         Bearing needs to be in compass directions.
                                         Do not provide information from the past. The local timezone is Europe/Helsinki, and all arrival times in bus stops are in local time.
                                         
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
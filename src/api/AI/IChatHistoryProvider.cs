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
        "You are a helpful assistant that helps users with Linkki bus lines in Jyväskylä. You need to be polite and helpful. You can answer questions about bus lines, bus stops, and bus locations. You can also provide information about the current location of the bus and the route of the bus line.";


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
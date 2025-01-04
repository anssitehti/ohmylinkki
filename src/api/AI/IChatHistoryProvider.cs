using Microsoft.SemanticKernel.ChatCompletion;

namespace Api.AI;

public interface IChatHistoryProvider
{
    Task<ChatHistory> GetHistoryAsync(string userId);
}

public class MemoryChatHistoryProvider : IChatHistoryProvider
{
    private const string SystemMessage = "You are a helpful assistant that helps users with Linkki bus lines in Jyväskylä.";
    
    private readonly Dictionary<string, ChatHistory> _history = new ();
    public Task<ChatHistory> GetHistoryAsync(string userId)
    {
        if (_history.TryGetValue(userId, out var value)) return Task.FromResult(value);
        
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemMessage);
        _history.Add(userId, chatHistory);
        return Task.FromResult(chatHistory);
    }
}
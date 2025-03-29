using FluentValidation;

namespace Api;

public record UserChatMessage(string Message, string UserId);

public record ClearChatHistory(string UserId);

public class UserChatMessageValidator : AbstractValidator<UserChatMessage> {
    public UserChatMessageValidator() {
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ClearChatHistoryValidator : AbstractValidator<ClearChatHistory> {
    public ClearChatHistoryValidator() {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
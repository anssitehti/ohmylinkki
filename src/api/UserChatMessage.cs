using FluentValidation;

namespace Api;

public record UserChatMessage(string Message, string UserId);


public class UserChatMessageValidator : AbstractValidator<UserChatMessage> {
    public UserChatMessageValidator() {
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

using CapFinLoan.Document.Application.Contracts.Requests;
using FluentValidation;

namespace CapFinLoan.Document.API.Validators;

public sealed class UpdateDocumentStatusRequestValidator : AbstractValidator<UpdateDocumentStatusRequest>
{
    private static readonly string[] AllowedStatuses =
        ["Processing", "Completed", "UnderReview", "Failed"];

    public UpdateDocumentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");

        RuleFor(x => x.FailureReason)
            .MaximumLength(2000)
            .WithMessage("FailureReason must not exceed 2000 characters.")
            .NotEmpty()
            .When(x => x.Status == "Failed")
            .WithMessage("FailureReason is required when Status is 'Failed'.");
    }
}

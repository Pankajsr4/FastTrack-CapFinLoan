using CapFinLoan.Admin.Application.Contracts.Requests;
using FluentValidation;

namespace CapFinLoan.Admin.API.Validators;

public sealed class ReviewLoanApplicationRequestValidator : AbstractValidator<ReviewLoanApplicationRequest>
{
    private static readonly string[] AllowedStatuses =
        ["Approved", "Rejected", "UnderReview", "PendingDocuments", "Disbursed"];

    public ReviewLoanApplicationRequestValidator()
    {
        RuleFor(x => x.TargetStatus)
            .NotEmpty()
            .WithMessage("TargetStatus is required.")
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"TargetStatus must be one of: {string.Join(", ", AllowedStatuses)}.");

        RuleFor(x => x.Remarks)
            .MaximumLength(1000)
            .WithMessage("Remarks must not exceed 1000 characters.");
    }
}

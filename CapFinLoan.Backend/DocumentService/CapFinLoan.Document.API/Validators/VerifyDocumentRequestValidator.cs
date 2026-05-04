using CapFinLoan.Document.Application.Contracts.Requests;
using FluentValidation;

namespace CapFinLoan.Document.API.Validators;

public sealed class VerifyDocumentRequestValidator : AbstractValidator<VerifyDocumentRequest>
{
    public VerifyDocumentRequestValidator()
    {
        RuleFor(x => x.Remarks)
            .MaximumLength(1000)
            .WithMessage("Remarks must not exceed 1000 characters.");
    }
}

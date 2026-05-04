using CapFinLoan.Admin.Application.Contracts.Requests;
using FluentValidation;

namespace CapFinLoan.Admin.API.Validators;

public sealed class DisburseApplicationRequestValidator : AbstractValidator<DisburseApplicationRequest>
{
    public DisburseApplicationRequestValidator()
    {
        RuleFor(x => x.DisbursedAmount)
            .GreaterThan(0)
            .WithMessage("Disbursed amount must be greater than zero.")
            .LessThanOrEqualTo(100_000_000)
            .WithMessage("Disbursed amount must not exceed 100,000,000.");
    }
}

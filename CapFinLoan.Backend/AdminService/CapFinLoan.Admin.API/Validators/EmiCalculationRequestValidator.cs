using CapFinLoan.Admin.Application.Contracts.Requests;
using FluentValidation;

namespace CapFinLoan.Admin.API.Validators;

public sealed class EmiCalculationRequestValidator : AbstractValidator<EmiCalculationRequest>
{
    public EmiCalculationRequestValidator()
    {
        RuleFor(x => x.LoanAmount)
            .GreaterThan(0)
            .WithMessage("Loan amount must be greater than zero.")
            .LessThanOrEqualTo(100_000_000)
            .WithMessage("Loan amount must not exceed 100,000,000.");

        RuleFor(x => x.InterestRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Interest rate must be zero or greater.")
            .LessThanOrEqualTo(100)
            .WithMessage("Interest rate must not exceed 100%.");

        RuleFor(x => x.TenureMonths)
            .GreaterThan(0)
            .WithMessage("Tenure must be at least 1 month.")
            .LessThanOrEqualTo(360)
            .WithMessage("Tenure must not exceed 360 months (30 years).");
    }
}

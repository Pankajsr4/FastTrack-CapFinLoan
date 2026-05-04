using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Contracts.Responses;
using CapFinLoan.Admin.Application.Interfaces;

namespace CapFinLoan.Admin.Application.Services;

public sealed class EmiCalculatorService : IEmiCalculatorService
{
    /// <summary>
    /// EMI = [P × R × (1+R)^N] / [(1+R)^N – 1]
    /// where R = monthly interest rate (annual / 12 / 100)
    ///       N = tenure in months
    ///       P = principal
    ///
    /// Uses double internally for Math.Pow, then rounds to 2 decimal places
    /// and converts back to decimal for all output values.
    /// </summary>
    public EmiCalculationResponse Calculate(EmiCalculationRequest request)
    {
        var p = request.LoanAmount;
        var n = request.TenureMonths;

        // Monthly rate as a fraction: e.g. 12% annual → 0.01 monthly
        var monthlyRate = (double)(request.InterestRate / 100m / 12m);

        decimal emi;

        if (monthlyRate == 0)
        {
            // Zero-interest edge case: simple division
            emi = Math.Round(p / n, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            var pow = Math.Pow(1 + monthlyRate, n);
            var rawEmi = (double)p * monthlyRate * pow / (pow - 1);
            emi = Math.Round((decimal)rawEmi, 2, MidpointRounding.AwayFromZero);
        }

        var totalPayment  = Math.Round(emi * n, 2, MidpointRounding.AwayFromZero);
        var totalInterest = Math.Round(totalPayment - p, 2, MidpointRounding.AwayFromZero);

        return new EmiCalculationResponse
        {
            LoanAmount         = p,
            AnnualInterestRate = request.InterestRate,
            TenureMonths       = n,
            MonthlyEmi         = emi,
            TotalPayment       = totalPayment,
            TotalInterest      = totalInterest
        };
    }
}

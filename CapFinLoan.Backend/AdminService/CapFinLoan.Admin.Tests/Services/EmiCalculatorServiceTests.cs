using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Services;
using FluentAssertions;

namespace CapFinLoan.Admin.Tests.Services;

/// <summary>
/// Unit tests for EmiCalculatorService.
/// Pure function — no mocks needed.
/// Formula: EMI = [P × R × (1+R)^N] / [(1+R)^N – 1]
/// </summary>
public class EmiCalculatorServiceTests
{
    private static readonly EmiCalculatorService Sut = new();

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_StandardLoan_ReturnsCorrectEmi()
    {
        // P=500000, R=12% p.a. (1% monthly), N=60 months
        // Expected EMI ≈ ₹11,122.22
        var result = Sut.Calculate(new() { LoanAmount = 500_000m, InterestRate = 12m, TenureMonths = 60 });

        result.MonthlyEmi.Should().BeApproximately(11_122.22m, 1m);
    }

    [Fact]
    public void Calculate_ReturnsCorrectTotalPayment()
    {
        var result = Sut.Calculate(new() { LoanAmount = 500_000m, InterestRate = 12m, TenureMonths = 60 });

        result.TotalPayment.Should().BeApproximately(result.MonthlyEmi * 60, 1m);
    }

    [Fact]
    public void Calculate_TotalInterest_EqualsTotalPaymentMinusPrincipal()
    {
        var result = Sut.Calculate(new() { LoanAmount = 300_000m, InterestRate = 10.5m, TenureMonths = 36 });

        result.TotalInterest.Should().BeApproximately(result.TotalPayment - result.LoanAmount, 1m);
    }

    [Fact]
    public void Calculate_ResponseContainsInputValues()
    {
        var req    = new EmiCalculationRequest { LoanAmount = 200_000m, InterestRate = 8.5m, TenureMonths = 24 };
        var result = Sut.Calculate(req);

        result.LoanAmount.Should().Be(200_000m);
        result.AnnualInterestRate.Should().Be(8.5m);
        result.TenureMonths.Should().Be(24);
    }

    // ── Zero interest edge case ───────────────────────────────────────────────

    [Fact]
    public void Calculate_ZeroInterestRate_EmiIsSimpleDivision()
    {
        // P=120000, R=0%, N=12 → EMI = 10000
        var result = Sut.Calculate(new() { LoanAmount = 120_000m, InterestRate = 0m, TenureMonths = 12 });

        result.MonthlyEmi.Should().Be(10_000m);
        result.TotalInterest.Should().Be(0m);
        result.TotalPayment.Should().Be(120_000m);
    }

    // ── Boundary values ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(10_000,    12,  6)]   // minimum loan, short tenure
    [InlineData(5_000_000, 18, 360)]  // maximum loan, long tenure
    [InlineData(100_000,   10, 120)]  // mid-range
    public void Calculate_BoundaryValues_DoesNotThrow(decimal amount, decimal rate, int months)
    {
        var act = () => Sut.Calculate(new() { LoanAmount = amount, InterestRate = rate, TenureMonths = months });
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(10_000,    12,  6)]
    [InlineData(5_000_000, 18, 360)]
    [InlineData(100_000,   10, 120)]
    public void Calculate_BoundaryValues_EmiIsPositive(decimal amount, decimal rate, int months)
    {
        var result = Sut.Calculate(new() { LoanAmount = amount, InterestRate = rate, TenureMonths = months });
        result.MonthlyEmi.Should().BePositive();
    }

    // ── Precision ─────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_EmiRoundedToTwoDecimalPlaces()
    {
        var result = Sut.Calculate(new() { LoanAmount = 333_333m, InterestRate = 11.75m, TenureMonths = 48 });

        // Verify no more than 2 decimal places
        var decimals = BitConverter.GetBytes(decimal.GetBits(result.MonthlyEmi)[3])[2];
        decimals.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public void Calculate_TotalPaymentRoundedToTwoDecimalPlaces()
    {
        var result = Sut.Calculate(new() { LoanAmount = 250_000m, InterestRate = 9.5m, TenureMonths = 36 });
        var decimals = BitConverter.GetBytes(decimal.GetBits(result.TotalPayment)[3])[2];
        decimals.Should().BeLessOrEqualTo(2);
    }

    // ── Higher rate = higher EMI ──────────────────────────────────────────────

    [Fact]
    public void Calculate_HigherInterestRate_ProducesHigherEmi()
    {
        var low  = Sut.Calculate(new() { LoanAmount = 500_000m, InterestRate = 8m,  TenureMonths = 60 });
        var high = Sut.Calculate(new() { LoanAmount = 500_000m, InterestRate = 18m, TenureMonths = 60 });

        high.MonthlyEmi.Should().BeGreaterThan(low.MonthlyEmi);
    }

    // ── Longer tenure = lower EMI but higher total interest ──────────────────

    [Fact]
    public void Calculate_LongerTenure_ProducesLowerEmiButHigherTotalInterest()
    {
        var short_ = Sut.Calculate(new() { LoanAmount = 500_000m, InterestRate = 12m, TenureMonths = 24 });
        var long_  = Sut.Calculate(new() { LoanAmount = 500_000m, InterestRate = 12m, TenureMonths = 120 });

        long_.MonthlyEmi.Should().BeLessThan(short_.MonthlyEmi);
        long_.TotalInterest.Should().BeGreaterThan(short_.TotalInterest);
    }
}

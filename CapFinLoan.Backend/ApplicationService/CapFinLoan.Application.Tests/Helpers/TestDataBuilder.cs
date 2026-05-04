using CapFinLoan.Application.Application.Contracts.Requests;
using CapFinLoan.Application.Domain.Constants;
using CapFinLoan.Application.Domain.Entities;

namespace CapFinLoan.Application.Tests.Helpers;

/// <summary>
/// Centralised factory for test data — keeps test bodies clean and
/// makes it easy to produce valid or deliberately invalid objects.
/// </summary>
public static class TestDataBuilder
{
    public static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultAppId  = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ── SaveLoanApplicationRequest ────────────────────────────────────────────

    /// <summary>Returns a fully valid request that passes all submission validation.</summary>
    public static SaveLoanApplicationRequest ValidRequest(
        decimal amount = 500_000m,
        int tenureMonths = 60,
        string purpose = "Personal Loan") => new()
    {
        PersonalDetails = new()
        {
            FirstName    = "Arjun",
            LastName     = "Sharma",
            DateOfBirth  = new DateTime(1990, 5, 15),
            Gender       = "Male",
            Email        = "arjun.sharma@example.com",
            Phone        = "9876543210",
            AddressLine1 = "12 MG Road",
            AddressLine2 = "Apt 4B",
            City         = "Bengaluru",
            State        = "Karnataka",
            PostalCode   = "560001"
        },
        EmploymentDetails = new()
        {
            EmployerName    = "Infosys Ltd",
            EmploymentType  = "Salaried",
            MonthlyIncome   = 80_000m,
            AnnualIncome    = 960_000m,
            ExistingEmiAmount = 5_000m
        },
        LoanDetails = new()
        {
            RequestedAmount       = amount,
            RequestedTenureMonths = tenureMonths,
            LoanPurpose           = purpose,
            Remarks               = "Home renovation"
        }
    };

    /// <summary>Returns a request with missing required personal fields.</summary>
    public static SaveLoanApplicationRequest RequestMissingPersonalDetails() => new()
    {
        PersonalDetails   = new() { FirstName = "", LastName = "", Email = "" },
        EmploymentDetails = new() { EmployerName = "Infosys", EmploymentType = "Salaried", MonthlyIncome = 80_000m, AnnualIncome = 960_000m },
        LoanDetails       = new() { RequestedAmount = 500_000m, RequestedTenureMonths = 60, LoanPurpose = "Personal Loan" }
    };

    // ── LoanApplication entity ────────────────────────────────────────────────

    /// <summary>Returns a fully populated Draft entity ready for submission.</summary>
    public static LoanApplication ValidDraftApplication(
        Guid? id = null,
        Guid? userId = null,
        decimal amount = 500_000m,
        int tenureMonths = 60) => new()
    {
        Id                    = id ?? DefaultAppId,
        ApplicantUserId       = userId ?? DefaultUserId,
        ApplicationNumber     = "APP-20260429-1234",
        Status                = ApplicationStatuses.Draft,
        FirstName             = "Arjun",
        LastName              = "Sharma",
        DateOfBirth           = new DateTime(1990, 5, 15),
        Gender                = "Male",
        Email                 = "arjun.sharma@example.com",
        Phone                 = "9876543210",
        AddressLine1          = "12 MG Road",
        City                  = "Bengaluru",
        State                 = "Karnataka",
        PostalCode            = "560001",
        EmployerName          = "Infosys Ltd",
        EmploymentType        = "Salaried",
        MonthlyIncome         = 80_000m,
        AnnualIncome          = 960_000m,
        ExistingEmiAmount     = 5_000m,
        RequestedAmount       = amount,
        RequestedTenureMonths = tenureMonths,
        LoanPurpose           = "Personal Loan",
        CreatedAtUtc          = DateTime.UtcNow,
        UpdatedAtUtc          = DateTime.UtcNow
    };

    /// <summary>Returns a Draft application with missing required fields (fails submission).</summary>
    public static LoanApplication IncompleteDraftApplication() => new()
    {
        Id                    = DefaultAppId,
        ApplicantUserId       = DefaultUserId,
        ApplicationNumber     = "APP-20260429-9999",
        Status                = ApplicationStatuses.Draft,
        FirstName             = "",          // missing
        LastName              = "",          // missing
        Email                 = "",          // missing
        Phone                 = "9876543210",
        AddressLine1          = "12 MG Road",
        City                  = "Bengaluru",
        State                 = "Karnataka",
        PostalCode            = "560001",
        EmployerName          = "Infosys Ltd",
        EmploymentType        = "Salaried",
        MonthlyIncome         = 80_000m,
        AnnualIncome          = 960_000m,
        RequestedAmount       = 500_000m,
        RequestedTenureMonths = 60,
        LoanPurpose           = "Personal Loan"
    };
}

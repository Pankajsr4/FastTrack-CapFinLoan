namespace CapFinLoan.Application.Domain.Constants;

public static class ApplicationStatuses
{
    public const string Draft        = "Draft";
    public const string Submitted    = "Submitted";
    public const string DocsPending  = "Docs Pending";
    public const string DocsVerified = "Docs Verified";
    public const string UnderReview  = "Under Review";
    public const string Approved     = "Approved";
    public const string Rejected     = "Rejected";
    public const string Closed       = "Closed";
    public const string Withdrawn    = "Withdrawn";

    /// <summary>Statuses where the applicant may still edit the application.</summary>
    public static readonly IReadOnlySet<string> EditableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Submitted, DocsPending, DocsVerified
    };

    /// <summary>Statuses where the applicant may withdraw the application.</summary>
    public static readonly IReadOnlySet<string> WithdrawableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Submitted, DocsPending, DocsVerified
    };
}
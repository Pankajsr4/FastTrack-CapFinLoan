namespace CapFinLoan.Admin.Application.Contracts.Requests;

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; init; }
}

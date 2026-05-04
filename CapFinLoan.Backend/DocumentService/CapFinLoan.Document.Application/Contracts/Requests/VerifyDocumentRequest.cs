namespace CapFinLoan.Document.Application.Contracts.Requests;

public class VerifyDocumentRequest
{
    public bool IsVerified { get; set; }
    public string? Remarks { get; set; }
}

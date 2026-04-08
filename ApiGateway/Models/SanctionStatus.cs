namespace ApiGateway.Models;

public enum SanctionStatus
{
    Draft = 0,
    Submitted = 1,
    Screening = 2,
    Underwriting = 3,
    Approved = 4,
    Rejected = 5,
    Disbursed = 6,
    Cancelled = 7
}

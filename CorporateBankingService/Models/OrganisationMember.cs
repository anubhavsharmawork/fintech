namespace CorporateBankingService.Models;

public class OrganisationMember
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string Role { get; set; } = "Viewer"; // Admin, Treasurer, Approver, Viewer
    public string Status { get; set; } = "Invited"; // Invited, Active, Removed
    public DateTime InvitedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public Organisation Organisation { get; set; } = null!;
}

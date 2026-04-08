using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Models.Dtos;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/sar")]
[Authorize]
public class SarController : ControllerBase
{
    private readonly TransactionDbContext _context;
    private readonly ILogger<SarController> _logger;

    public SarController(TransactionDbContext context, ILogger<SarController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSarReports()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var reports = await _context.SuspiciousActivityReports
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.FlaggedAt)
            .Select(r => new SarSummaryDto
            {
                Id = r.Id,
                TransactionId = r.TransactionId,
                UserId = r.UserId,
                Amount = r.Amount,
                Currency = r.Currency,
                Reason = r.Reason,
                RiskLevel = r.RiskLevel,
                FlaggedAt = r.FlaggedAt,
                Status = r.Status
            })
            .ToListAsync();

        return Ok(reports);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSarReport(Guid id)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var report = await _context.SuspiciousActivityReports
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (report is null)
            return NotFound();

        return Ok(new SarSummaryDto
        {
            Id = report.Id,
            TransactionId = report.TransactionId,
            UserId = report.UserId,
            Amount = report.Amount,
            Currency = report.Currency,
            Reason = report.Reason,
            RiskLevel = report.RiskLevel,
            FlaggedAt = report.FlaggedAt,
            Status = report.Status
        });
    }

    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Roles = "ComplianceOfficer,Admin")]
    public async Task<IActionResult> ResolveSar(Guid id, [FromBody] ResolveSarDto dto)
    {
        var report = await _context.SuspiciousActivityReports.FindAsync(id);
        if (report is null)
            return NotFound();

        report.Status = "Resolved";
        report.ResolvedAt = DateTime.UtcNow;
        report.Notes = dto.Notes;

        await _context.SaveChangesAsync();

        _logger.LogInformation("SAR resolved: {SarId}, Notes: {Notes}", id, dto.Notes);

        return Ok(new SarSummaryDto
        {
            Id = report.Id,
            TransactionId = report.TransactionId,
            UserId = report.UserId,
            Amount = report.Amount,
            Currency = report.Currency,
            Reason = report.Reason,
            RiskLevel = report.RiskLevel,
            FlaggedAt = report.FlaggedAt,
            Status = report.Status
        });
    }
}

public class ResolveSarDto
{
    public string Notes { get; set; } = string.Empty;
}

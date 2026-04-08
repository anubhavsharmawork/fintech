using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using CorporateBankingService.Data;
using CorporateBankingService.Models;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Services;
using Contracts.Events;

namespace CorporateBankingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentBatchesController : ControllerBase
{
    private readonly CorporateDbContext _context;
    private readonly IApprovalWorkflowService _approvalWorkflow;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentBatchesController> _logger;

    public PaymentBatchesController(
        CorporateDbContext context,
        IApprovalWorkflowService approvalWorkflow,
        IPublishEndpoint publishEndpoint,
        IHttpClientFactory httpClientFactory,
        ILogger<PaymentBatchesController> logger)
    {
        _context = context;
        _approvalWorkflow = approvalWorkflow;
        _publishEndpoint = publishEndpoint;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetBatches()
    {
        if (!TryGetOrganisationId(out var orgId))
            return Forbid();

        var batches = await _context.PaymentBatches
            .Where(b => b.OrganisationId == orgId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return Ok(batches.Select(b => new PaymentBatchResponse(
            b.Id, b.OrganisationId, b.SubmittedByUserId, b.Status, b.Currency,
            b.TotalAmount, b.ItemCount, b.CreatedAt, b.SubmittedAt, b.ExecutedAt)));
    }

    [HttpGet("{batchId}")]
    public async Task<IActionResult> GetBatch(Guid batchId)
    {
        if (!TryGetOrganisationId(out var orgId))
            return Forbid();

        var batch = await _context.PaymentBatches
            .Include(b => b.Items)
            .Include(b => b.Approvals)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganisationId == orgId);

        if (batch is null) return NotFound();

        return Ok(new PaymentBatchDetailResponse(
            batch.Id, batch.OrganisationId, batch.SubmittedByUserId, batch.Status,
            batch.Currency, batch.TotalAmount, batch.ItemCount, batch.CreatedAt,
            batch.SubmittedAt, batch.ExecutedAt,
            batch.Items.Select(i => new PaymentBatchItemDto(
                i.SourceAccountId, i.PayeeName, i.PayeeAccountNumber, i.Amount, i.Description)).ToList(),
            batch.Approvals.Select(a => new ApprovalRecordResponse(
                a.Id, a.ApprovedByUserId, a.Decision, a.Comments, a.DecidedAt)).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreatePaymentBatchRequest request)
    {
        if (!TryGetOrganisationId(out var orgId))
            return Forbid();

        if (!IsCallerInRole("Admin", "Treasurer"))
            return Forbid();

        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var batch = new PaymentBatch
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            SubmittedByUserId = userId,
            Status = "Draft",
            Currency = request.Currency.Trim().ToUpperInvariant(),
            TotalAmount = request.Items.Sum(i => i.Amount),
            ItemCount = request.Items.Count,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentBatches.Add(batch);

        foreach (var item in request.Items)
        {
            _context.PaymentBatchItems.Add(new PaymentBatchItem
            {
                Id = Guid.NewGuid(),
                PaymentBatchId = batch.Id,
                SourceAccountId = item.SourceAccountId,
                PayeeName = item.PayeeName.Trim(),
                PayeeAccountNumber = item.PayeeAccountNumber?.Trim(),
                Amount = item.Amount,
                Description = item.Description?.Trim()
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment batch created: {BatchId} for org {OrgId}, {ItemCount} items, total {Total}",
            batch.Id, orgId, batch.ItemCount, batch.TotalAmount);

        return Ok(new PaymentBatchResponse(
            batch.Id, batch.OrganisationId, batch.SubmittedByUserId, batch.Status,
            batch.Currency, batch.TotalAmount, batch.ItemCount, batch.CreatedAt,
            batch.SubmittedAt, batch.ExecutedAt));
    }

    [HttpPost("{batchId}/submit")]
    public async Task<IActionResult> SubmitForApproval(Guid batchId)
    {
        if (!TryGetOrganisationId(out var orgId))
            return Forbid();

        if (!IsCallerInRole("Admin", "Treasurer"))
            return Forbid();

        var batch = await _context.PaymentBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganisationId == orgId);

        if (batch is null) return NotFound();

        if (batch.Status != "Draft")
            return BadRequest(new { message = "Only draft batches can be submitted for approval." });

        batch.Status = "PendingApproval";
        batch.SubmittedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _publishEndpoint.Publish(new PaymentBatchSubmittedForApproval(
            batch.Id, orgId, batch.SubmittedByUserId, batch.ItemCount,
            batch.TotalAmount, batch.Currency, batch.SubmittedAt.Value));

        _logger.LogInformation("Batch {BatchId} submitted for approval in org {OrgId}", batchId, orgId);

        return Ok(new PaymentBatchResponse(
            batch.Id, batch.OrganisationId, batch.SubmittedByUserId, batch.Status,
            batch.Currency, batch.TotalAmount, batch.ItemCount, batch.CreatedAt,
            batch.SubmittedAt, batch.ExecutedAt));
    }

    [HttpPost("{batchId}/execute")]
    public async Task<IActionResult> ExecuteBatch(Guid batchId)
    {
        if (!TryGetOrganisationId(out var orgId))
            return Forbid();

        if (!IsCallerInRole("Admin", "Treasurer"))
            return Forbid();

        var batch = await _context.PaymentBatches
            .Include(b => b.Items)
            .Include(b => b.Approvals)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganisationId == orgId);

        if (batch is null) return NotFound();

        if (batch.Status != "Approved")
            return BadRequest(new { message = "Only approved batches can be executed." });

        var client = _httpClientFactory.CreateClient("transactions");
        var token = HttpContext.Request.Headers["Authorization"].ToString();

        foreach (var item in batch.Items)
        {
            var txRequest = new
            {
                accountId = item.SourceAccountId,
                amount = item.Amount,
                type = "debit",
                currency = batch.Currency,
                payeeName = item.PayeeName,
                payeeAccountNumber = item.PayeeAccountNumber,
                description = item.Description ?? $"Batch payment to {item.PayeeName}",
                spendingType = "Fixed"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions");
            request.Headers.Add("Authorization", token);
            request.Content = JsonContent.Create(txRequest);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to execute batch item {ItemId} in batch {BatchId}: {StatusCode}",
                    item.Id, batchId, response.StatusCode);
            }
        }

        batch.Status = "Executed";
        batch.ExecutedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _publishEndpoint.Publish(new BatchExecuted(
            batch.Id, orgId, batch.ItemCount, batch.TotalAmount, batch.Currency, batch.ExecutedAt.Value));

        _logger.LogInformation("Batch {BatchId} executed in org {OrgId}: {ItemCount} payments totalling {Total}",
            batchId, orgId, batch.ItemCount, batch.TotalAmount);

        return Ok(new PaymentBatchResponse(
            batch.Id, batch.OrganisationId, batch.SubmittedByUserId, batch.Status,
            batch.Currency, batch.TotalAmount, batch.ItemCount, batch.CreatedAt,
            batch.SubmittedAt, batch.ExecutedAt));
    }

    private bool TryGetOrganisationId(out Guid orgId)
    {
        return Guid.TryParse(User.FindFirst("organisation_id")?.Value, out orgId);
    }

    private bool IsCallerInRole(params string[] roles)
    {
        var callerRole = User.FindFirst("organisation_role")?.Value;
        return roles.Any(r => string.Equals(callerRole, r, StringComparison.OrdinalIgnoreCase));
    }
}

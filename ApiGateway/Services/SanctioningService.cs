using ApiGateway.Configuration;
using ApiGateway.Data;
using ApiGateway.Models;
using ApiGateway.Models.Dtos;
using ApiGateway.Services.Underwriting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services;

public class SanctioningService : ISanctioningService
{
    private readonly SanctionDbContext _db;
    private readonly IKycService _kycService;
    private readonly IAmlService _amlService;
    private readonly IFtkLedgerService _ftkLedgerService;
    private readonly IEnumerable<IUnderwritingRule> _underwritingRules;
    private readonly UnderwritingSettings _settings;
    private readonly ILogger<SanctioningService> _logger;

    private static readonly Dictionary<SanctionStatus, HashSet<SanctionStatus>> AllowedTransitions = new()
    {
        [SanctionStatus.Draft] = new() { SanctionStatus.Submitted, SanctionStatus.Cancelled },
        [SanctionStatus.Submitted] = new() { SanctionStatus.Screening, SanctionStatus.Cancelled },
        [SanctionStatus.Screening] = new() { SanctionStatus.Underwriting, SanctionStatus.Rejected },
        [SanctionStatus.Underwriting] = new() { SanctionStatus.Approved, SanctionStatus.Rejected },
        [SanctionStatus.Approved] = new() { SanctionStatus.Disbursed },
        [SanctionStatus.Rejected] = new(),
        [SanctionStatus.Disbursed] = new(),
        [SanctionStatus.Cancelled] = new()
    };

    public SanctioningService(
        SanctionDbContext db,
        IKycService kycService,
        IAmlService amlService,
        IFtkLedgerService ftkLedgerService,
        IEnumerable<IUnderwritingRule> underwritingRules,
        IOptions<UnderwritingSettings> settings,
        ILogger<SanctioningService> logger)
    {
        _db = db;
        _kycService = kycService;
        _amlService = amlService;
        _ftkLedgerService = ftkLedgerService;
        _underwritingRules = underwritingRules;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<SanctionRequest> CreateSanctionRequestAsync(
        CreateSanctionRequestDto dto, string createdBy, CancellationToken ct = default)
    {
        var existing = await _db.SanctionRequests
            .FirstOrDefaultAsync(r => r.IdempotencyKey == dto.IdempotencyKey, ct);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotent duplicate detected for key {IdempotencyKey}, returning existing {SanctionId}",
                dto.IdempotencyKey, existing.Id);
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var request = new SanctionRequest
        {
            Id = Guid.NewGuid(),
            ExternalProjectId = dto.ExternalProjectId,
            ExternalTenantId = dto.ExternalTenantId,
            UserId = dto.UserId,
            AccountId = dto.AccountId,
            RequestedAmount = dto.RequestedAmount,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "FTK" : dto.Currency,
            Purpose = dto.Purpose,
            RiskScore = 0,
            KycStatus = KycStatus.Pending,
            AmlStatus = AmlStatus.Pending,
            Status = SanctionStatus.Submitted,
            IdempotencyKey = dto.IdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy
        };

        _db.SanctionRequests.Add(request);

        var auditLog = new SanctionAuditLog
        {
            Id = Guid.NewGuid(),
            SanctionRequestId = request.Id,
            FromStatus = SanctionStatus.Draft,
            ToStatus = SanctionStatus.Submitted,
            ChangedBy = createdBy,
            Reason = "Sanction request created",
            Timestamp = now,
            CorrelationId = BuildCorrelationId(request)
        };
        _db.SanctionAuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SanctionRequest created: {SanctionId} for project {ProjectId}, user {UserId}",
            request.Id, request.ExternalProjectId, request.UserId);

        return request;
    }

    public async Task<SanctionRequest> RunScreeningAsync(
        Guid sanctionRequestId, string changedBy, CancellationToken ct = default)
    {
        var request = await GetRequiredAsync(sanctionRequestId, ct);

        TransitionStatus(request, SanctionStatus.Screening, changedBy, "Screening started");
        await _db.SaveChangesAsync(ct);

        try
        {
            var kycResult = await _kycService.ValidateAsync(request.UserId, ct);
            request.KycStatus = kycResult;

            _logger.LogInformation(
                "KYC result for {SanctionId}: {KycStatus}",
                sanctionRequestId, kycResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "KYC validation failed for {SanctionId}. Keeping Pending status.",
                sanctionRequestId);
        }

        try
        {
            var amlResult = await _amlService.ScreenAsync(
                request.UserId, request.RequestedAmount, request.Purpose, ct);
            request.AmlStatus = amlResult;

            _logger.LogInformation(
                "AML result for {SanctionId}: {AmlStatus}",
                sanctionRequestId, amlResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AML screening failed for {SanctionId}. Keeping Pending status.",
                sanctionRequestId);
        }

        if (request.KycStatus == KycStatus.Failed || request.AmlStatus == AmlStatus.Flagged)
        {
            var reason = request.KycStatus == KycStatus.Failed
                ? "KYC validation failed"
                : "AML screening flagged";

            request.DecisionReason = reason;
            TransitionStatus(request, SanctionStatus.Rejected, changedBy, reason);
        }
        else if (request.KycStatus == KycStatus.Passed && request.AmlStatus == AmlStatus.Passed)
        {
            TransitionStatus(request, SanctionStatus.Underwriting, changedBy, "KYC and AML passed");
        }

        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<SanctionRequest> RunUnderwritingAsync(
        Guid sanctionRequestId, string changedBy, CancellationToken ct = default)
    {
        var request = await GetRequiredAsync(sanctionRequestId, ct);

        if (request.Status != SanctionStatus.Underwriting)
        {
            throw new InvalidOperationException(
                $"Cannot run underwriting on request in status {request.Status}");
        }

        var priorApprovedCount = await _db.SanctionRequests
            .CountAsync(r =>
                r.UserId == request.UserId &&
                r.Id != request.Id &&
                (r.Status == SanctionStatus.Approved || r.Status == SanctionStatus.Disbursed), ct);

        var riskScore = 0;
        foreach (var rule in _underwritingRules)
        {
            var points = rule.Evaluate(request, priorApprovedCount);
            riskScore += points;
        }

        request.RiskScore = Math.Max(0, Math.Min(100, riskScore));

        _logger.LogInformation(
            "Underwriting complete for {SanctionId}: RiskScore={RiskScore}, Threshold={Threshold}",
            sanctionRequestId, request.RiskScore, _settings.MaxAcceptableRiskScore);

        if (request.RiskScore > _settings.MaxAcceptableRiskScore)
        {
            request.DecisionReason = $"Risk score {request.RiskScore} exceeds threshold {_settings.MaxAcceptableRiskScore}";
            TransitionStatus(request, SanctionStatus.Rejected, changedBy, request.DecisionReason);
        }
        else
        {
            var discount = _settings.PartialApprovalDiscountPercent;
            request.ApprovedAmount = discount > 0
                ? request.RequestedAmount * (1 - discount / 100m)
                : request.RequestedAmount;

            request.DecisionReason = $"Approved with risk score {request.RiskScore}";
            TransitionStatus(request, SanctionStatus.Approved, changedBy, request.DecisionReason);
        }

        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<SanctionRequest> DisburseToFtkAsync(
        Guid sanctionRequestId, string changedBy, CancellationToken ct = default)
    {
        var request = await GetRequiredAsync(sanctionRequestId, ct);

        if (request.Status != SanctionStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Cannot disburse request in status {request.Status}. Must be Approved.");
        }

        try
        {
            var txRef = await _ftkLedgerService.AllocateAsync(
                request.UserId, request.AccountId, request.ApprovedAmount ?? request.RequestedAmount, ct);

            request.FtkTransactionRef = txRef;

            _logger.LogInformation(
                "FTK disbursement complete for {SanctionId}: TxRef={TxRef}",
                sanctionRequestId, txRef);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FTK allocation failed for {SanctionId}. Request state unchanged.",
                sanctionRequestId);
            throw;
        }

        TransitionStatus(request, SanctionStatus.Disbursed, changedBy, "FTK tokens allocated");
        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<SanctionRequest> RejectRequestAsync(
        Guid sanctionRequestId, string reason, string changedBy, CancellationToken ct = default)
    {
        var request = await GetRequiredAsync(sanctionRequestId, ct);
        request.DecisionReason = reason;
        TransitionStatus(request, SanctionStatus.Rejected, changedBy, reason);
        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<SanctionRequest> CancelRequestAsync(
        Guid sanctionRequestId, string reason, string changedBy, CancellationToken ct = default)
    {
        var request = await GetRequiredAsync(sanctionRequestId, ct);
        request.DecisionReason = reason;
        TransitionStatus(request, SanctionStatus.Cancelled, changedBy, reason);
        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<SanctionRequest?> GetSanctionStatusAsync(
        string externalProjectId, Guid userId, CancellationToken ct = default)
    {
        return await _db.SanctionRequests
            .Where(r => r.ExternalProjectId == externalProjectId && r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SanctionRequest?> GetByIdAsync(
        Guid sanctionRequestId, CancellationToken ct = default)
    {
        return await _db.SanctionRequests.FindAsync(new object[] { sanctionRequestId }, ct);
    }

    public async Task<List<SanctionAuditLog>> GetAuditLogsAsync(
        Guid sanctionRequestId, CancellationToken ct = default)
    {
        return await _db.SanctionAuditLogs
            .Where(l => l.SanctionRequestId == sanctionRequestId)
            .OrderBy(l => l.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<SanctionRequest>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.SanctionRequests
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    private void TransitionStatus(
        SanctionRequest request, SanctionStatus toStatus, string changedBy, string reason)
    {
        var fromStatus = request.Status;

        if (!AllowedTransitions.TryGetValue(fromStatus, out var allowed) || !allowed.Contains(toStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {fromStatus} to {toStatus}");
        }

        request.Status = toStatus;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        var auditLog = new SanctionAuditLog
        {
            Id = Guid.NewGuid(),
            SanctionRequestId = request.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedBy = changedBy,
            Reason = reason,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = BuildCorrelationId(request)
        };

        _db.SanctionAuditLogs.Add(auditLog);

        _logger.LogInformation(
            "SanctionRequest {SanctionId} transitioned from {FromStatus} to {ToStatus} by {ChangedBy}. " +
            "ProjectId={ProjectId}, UserId={UserId}, RiskScore={RiskScore}, CorrelationId={CorrelationId}",
            request.Id, fromStatus, toStatus, changedBy,
            request.ExternalProjectId, request.UserId, request.RiskScore,
            auditLog.CorrelationId);
    }

    private async Task<SanctionRequest> GetRequiredAsync(Guid id, CancellationToken ct)
    {
        return await _db.SanctionRequests.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException($"SanctionRequest {id} not found");
    }

    private static string BuildCorrelationId(SanctionRequest request) =>
        $"{request.ExternalProjectId}|{request.UserId}|{request.ExternalTenantId}";
}
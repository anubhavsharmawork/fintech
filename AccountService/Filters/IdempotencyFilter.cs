using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using AccountService.Data;
using AccountService.Models;

namespace AccountService.Filters;

/// <summary>
/// Action filter that enforces idempotency for POST endpoints that create accounts or modify balances.
/// Requires an Idempotency-Key header. Returns stored response on duplicate keys.
/// Returns HTTP 409 if the original request is still in flight.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class IdempotencyFilterAttribute : Attribute, IAsyncActionFilter
{
    private const int IdempotencyKeyTtlHours = 24;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;

        // Only enforce on POST/PUT/PATCH methods
        if (!HttpMethods.IsPost(request.Method) && 
            !HttpMethods.IsPut(request.Method) && 
            !HttpMethods.IsPatch(request.Method))
        {
            await next();
            return;
        }

        // Check for Idempotency-Key header
        if (!request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) ||
            string.IsNullOrWhiteSpace(idempotencyKeyValues.FirstOrDefault()))
        {
            context.Result = new BadRequestObjectResult(new { error = "Idempotency-Key header is required for this operation." });
            return;
        }

        var idempotencyKey = idempotencyKeyValues.First()!.Trim();
        if (idempotencyKey.Length > 64)
        {
            context.Result = new BadRequestObjectResult(new { error = "Idempotency-Key must be 64 characters or less." });
            return;
        }

        var dbContext = httpContext.RequestServices.GetRequiredService<AccountDbContext>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<IdempotencyFilterAttribute>>();

        // Clean up expired records (fire-and-forget, best effort)
        _ = CleanupExpiredRecordsAsync(dbContext, logger);

        // Check if this key already exists
        var existingRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey);

        if (existingRecord != null)
        {
            // If still processing, return 409 Conflict
            if (existingRecord.IsProcessing)
            {
                logger.LogWarning("Idempotency key {Key} is still being processed", idempotencyKey);
                context.Result = new ConflictObjectResult(new { error = "A request with this Idempotency-Key is currently being processed. Please retry later." });
                return;
            }

            // Return cached response
            logger.LogInformation("Returning cached response for idempotency key {Key}", idempotencyKey);
            context.Result = new ContentResult
            {
                Content = existingRecord.ResponseBody,
                ContentType = "application/json",
                StatusCode = existingRecord.ResponseStatusCode
            };
            return;
        }

        // Create a new record in "processing" state
        var newRecord = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            RequestPath = request.Path.Value ?? "",
            RequestMethod = request.Method,
            ResponseBody = "",
            ResponseStatusCode = 0,
            IsProcessing = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(IdempotencyKeyTtlHours)
        };

        try
        {
            dbContext.IdempotencyRecords.Add(newRecord);
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Race condition: another request just inserted this key
            logger.LogWarning("Race condition detected for idempotency key {Key}", idempotencyKey);
            context.Result = new ConflictObjectResult(new { error = "A request with this Idempotency-Key is currently being processed. Please retry later." });
            return;
        }

        // Execute the action
        var executedContext = await next();

        // Store the response
        try
        {
            var statusCode = 200;
            string responseBody = "{}";

            if (executedContext.Result is ObjectResult objectResult)
            {
                statusCode = objectResult.StatusCode ?? 200;
                responseBody = JsonSerializer.Serialize(objectResult.Value);
            }
            else if (executedContext.Result is ContentResult contentResult)
            {
                statusCode = contentResult.StatusCode ?? 200;
                responseBody = contentResult.Content ?? "{}";
            }

            newRecord.ResponseBody = responseBody;
            newRecord.ResponseStatusCode = statusCode;
            newRecord.IsProcessing = false;

            dbContext.IdempotencyRecords.Update(newRecord);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Stored idempotency response for key {Key} with status {Status}", idempotencyKey, statusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store idempotency response for key {Key}", idempotencyKey);
            // Don't fail the request if we can't store the response
        }
    }

    private static async Task CleanupExpiredRecordsAsync(AccountDbContext dbContext, ILogger logger)
    {
        try
        {
            var expiredRecords = await dbContext.IdempotencyRecords
                .Where(r => r.ExpiresAt < DateTime.UtcNow)
                .Take(100) // Limit batch size
                .ToListAsync();

            if (expiredRecords.Count > 0)
            {
                dbContext.IdempotencyRecords.RemoveRange(expiredRecords);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Cleaned up {Count} expired idempotency records", expiredRecords.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cleanup expired idempotency records");
        }
    }
}

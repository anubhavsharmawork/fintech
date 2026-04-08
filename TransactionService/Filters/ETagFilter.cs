using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TransactionService.Filters;

/// <summary>
/// Action filter that computes SHA-256 hash of response body and returns it as ETag header.
/// Returns HTTP 304 Not Modified if If-None-Match header matches current ETag.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class ETagFilterAttribute : Attribute, IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;

        // Only apply to GET requests
        if (!HttpMethods.IsGet(request.Method))
        {
            await next();
            return;
        }

        // Get the response body
        string responseBody;
        if (context.Result is ObjectResult objectResult)
        {
            responseBody = JsonSerializer.Serialize(objectResult.Value);
        }
        else if (context.Result is ContentResult contentResult)
        {
            responseBody = contentResult.Content ?? "";
        }
        else
        {
            await next();
            return;
        }

        // Compute SHA-256 hash
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(responseBody));
        var etag = $"\"{Convert.ToBase64String(hash)}\"";

        // Check If-None-Match header
        if (request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch))
        {
            var clientEtag = ifNoneMatch.FirstOrDefault()?.Trim();
            if (clientEtag == etag)
            {
                context.Result = new StatusCodeResult(304);
                response.Headers["ETag"] = etag;
                return;
            }
        }

        // Set ETag header and continue
        response.Headers["ETag"] = etag;
        await next();
    }
}

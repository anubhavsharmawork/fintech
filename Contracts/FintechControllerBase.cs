using System;
using Microsoft.AspNetCore.Mvc;

namespace Contracts
{
    public sealed class MissingClaimUnauthorizedException : UnauthorizedAccessException
    {
        public MissingClaimUnauthorizedException(string message) : base(message)
        {
        }
    }

    public abstract class FintechControllerBase : ControllerBase
    {
        protected Guid CurrentUserId
        {
            get
            {
                var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                    throw new MissingClaimUnauthorizedException("Missing or invalid user claim");
                return userId;
            }
        }

        protected Guid CurrentOrganisationId
        {
            get
            {
                var callerOrgId = User.FindFirst("organisation_id")?.Value;
                if (string.IsNullOrEmpty(callerOrgId) || !Guid.TryParse(callerOrgId, out var orgId))
                    throw new MissingClaimUnauthorizedException("Missing or invalid organisation claim");
                return orgId;
            }
        }
    }
}

using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace ArtAround.Api.Authorization;

public class OwnerRequirement : IAuthorizationRequirement;

/// <summary>Resource-based: runs when an action passes the loaded resource to IAuthorizationService.</summary>
public class OwnerHandler : AuthorizationHandler<OwnerRequirement, IOwned>
{
    public const string Policy = "Owner";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnerRequirement requirement, IOwned resource)
    {
        if (context.User.FindId() == resource.CreatorId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

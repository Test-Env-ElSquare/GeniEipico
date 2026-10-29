using System.Security.Claims;
using API.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.SignalR;

namespace API.Realtime.Signage;

public static class SignageAccess
{
    public static ScadaLiveViewScope Resolve(ClaimsPrincipal user, IUnitOfWork uow, int factoryId)
    {
        if (!AuthenticatedAccessScope.TryResolve(user, uow, out var access, out _, out var message))
            throw new HubException(message);
        if (factoryId <= 0 || !access.CanAccessFactory(factoryId))
            throw new HubException("Factory is outside the authenticated user's access scope.");
        return new(access.CustomerId, factoryId, null,
            access.LineFactoryIds.Where(x => x.Value == factoryId).Select(x => x.Key).ToHashSet());
    }
}
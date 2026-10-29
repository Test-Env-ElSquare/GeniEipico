using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Dtos.Overview;

namespace API.Common;

public sealed class OverviewAccessProvider(IHttpContextAccessor http, IUnitOfWork uow) : IOverviewAccessProvider
{
    public OverviewAccess? GetAccess()
        => http.HttpContext?.User is { } user && AuthenticatedAccessScope.TryResolve(user, uow, out var scope, out _, out _)
            ? new(scope.CustomerId, scope.FactoryIds, scope.LineFactoryIds) : null;
}

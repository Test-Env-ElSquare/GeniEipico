using Application.Common.Paging;
using Domain.Common;

namespace Application.Abstractions.Services;

public interface ICrudService<TEntity> where TEntity : Entity
{
    IQueryable<TEntity> Query(bool asNoTracking = true);

    Task<TEntity?> GetByIdAsync(int id);

    Task<IReadOnlyList<TEntity>> GetAllAsync();

    Task<PagedResult<TEntity>> GetPagedAsync(QueryParameters parameters);

    Task<TEntity> CreateAsync(TEntity entity);

    Task<TEntity?> UpdateAsync(int id, TEntity entity);

    Task<bool> DeleteAsync(int id);
}

using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Paging;
using Domain.Common;

namespace Application.Services;

public class CrudService<TEntity> : ICrudService<TEntity> where TEntity : Entity
{
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly IRepository<TEntity> Repository;

    public CrudService(IUnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;
        Repository = unitOfWork.Repository<TEntity>();
    }

    public virtual IQueryable<TEntity> Query(bool asNoTracking = true)
    {
        return Repository.Query(asNoTracking);
    }

    public virtual Task<TEntity?> GetByIdAsync(int id)
    {
        return Repository.GetByIdAsync(id, asNoTracking: true);
    }

    public virtual Task<IReadOnlyList<TEntity>> GetAllAsync()
    {
        return Repository.GetAllAsync(asNoTracking: true);
    }

    public virtual Task<PagedResult<TEntity>> GetPagedAsync(QueryParameters parameters)
    {
        return Repository.GetPagedAsync(parameters);
    }

    public virtual async Task<TEntity> CreateAsync(TEntity entity)
    {
        await Repository.AddAsync(entity);
        await UnitOfWork.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<TEntity?> UpdateAsync(int id, TEntity entity)
    {
        var exists = await Repository.AnyAsync(x => x.Id == id);
        if (!exists)
        {
            return null;
        }

        entity.Id = id;
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(int id)
    {
        var entity = await Repository.GetByIdAsync(id, asNoTracking: false);
        if (entity is null)
        {
            return false;
        }

        Repository.Remove(entity);
        await UnitOfWork.SaveChangesAsync();
        return true;
    }
}

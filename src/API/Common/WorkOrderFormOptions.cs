using Application.Abstractions.Repositories;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace API.Common;

public static class WorkOrderFormOptions
{
    public static object Build(IUnitOfWork uow, UserManager<ApplicationUser> users,
        AuthenticatedAccessScope scope)
    {
        var factories = uow.Factories.Query()
            .Where(x => scope.FactoryIds.Contains(x.Id) && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new { value = x.Id, label = x.Name, x.Code }).ToArray();
        var machines = (
            from machine in uow.Machines.Query()
            join line in uow.ProductionLines.Query() on machine.LineId equals line.Id
            where scope.LineIds.Contains(line.Id) && machine.IsActive && !machine.IsDeleted
            orderby machine.Name
            select new { value = machine.Id, label = machine.Name, machine.Code,
                line.FactoryId, machine.LineId }).ToArray();
        var technicians = users.Users
            .Where(x => x.CustomerId == scope.CustomerId && x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new { value = x.Id, label = x.FullName, x.Email }).ToArray();
        static object[] Options<TEnum>() where TEnum : struct, Enum
            => Enum.GetValues<TEnum>().Select(value => new
            {
                value,
                label = string.Concat(value.ToString().Select((character, index) =>
                    index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()))
            }).Cast<object>().ToArray();
        return new
        {
            factories, machines, technicians,
            workOrderTypes = Options<WorkOrderType>(),
            priorities = Options<WorkOrderPriority>(),
            statuses = Options<WorkOrderStatus>(),
            defaults = new
            {
                workOrderType = WorkOrderType.Corrective,
                priority = WorkOrderPriority.Medium,
                status = WorkOrderStatus.Open
            }
        };
    }
}

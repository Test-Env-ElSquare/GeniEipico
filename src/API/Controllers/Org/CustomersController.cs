using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Organization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Org;

[Route("v1/customers")]
public class CustomersController : CrudController<Customer, ICustomerService>
{
    public CustomersController(ICustomerService service) : base(service)
    {
    }

}

using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "Customer,Agent,Admin")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _customers;

    public CustomersController(ICustomerRepository customers)
    {
        _customers = customers;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Customer") &&
            !string.Equals(User.GetCustomerId(), id, StringComparison.Ordinal))
        {
            return Forbid();
        }

        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return NotFound(new { status = 404, error = $"Cliente {id} não encontrado." });
        }

        return Ok(customer.ToDto());
    }
}

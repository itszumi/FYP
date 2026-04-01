using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementAPI.Data;
using OrderManagementAPI.Models;

namespace OrderManagementAPI.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db) => _db = db;

    // GET /api/customers
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _db.Customers.ToListAsync();
        return Ok(customers);
    }

    // GET /api/customers/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        return Ok(customer);
    }

    // POST /api/customers
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Customer customer)
    {
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    // PATCH /api/customers/{id}
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Customer updated)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(updated.Name)) customer.Name = updated.Name;
        if (!string.IsNullOrWhiteSpace(updated.Email)) customer.Email = updated.Email;
        if (!string.IsNullOrWhiteSpace(updated.Phone)) customer.Phone = updated.Phone;

        await _db.SaveChangesAsync();
        return Ok(customer);
    }

    // DELETE /api/customers/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

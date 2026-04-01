using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementAPI.Data;
using OrderManagementAPI.Models;

namespace OrderManagementAPI.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db) => _db = db;

    // GET /api/orders
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .ToListAsync();
        return Ok(orders);
    }

    // GET /api/orders/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        return Ok(order);
    }

    // GET /api/orders/customer/{customerId}
    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var orders = await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.OrderItems)
            .ToListAsync();
        return Ok(orders);
    }

    // POST /api/orders
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Order order)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == order.CustomerId);
        if (!customerExists) return BadRequest("Customer not found.");

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    // PATCH /api/orders/{id}
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Order updated)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(updated.Status)) order.Status = updated.Status;
        if (updated.OrderDate != default) order.OrderDate = updated.OrderDate;

        await _db.SaveChangesAsync();
        return Ok(order);
    }

    // DELETE /api/orders/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

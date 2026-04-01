using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementAPI.Data;
using OrderManagementAPI.Models;

namespace OrderManagementAPI.Controllers;

[ApiController]
[Route("api/orderitems")]
public class OrderItemsController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrderItemsController(AppDbContext db) => _db = db;

    // GET /api/orderitems
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.OrderItems
            .Include(oi => oi.Order)
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/orderitems/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.OrderItems
            .Include(oi => oi.Order)
            .FirstOrDefaultAsync(oi => oi.Id == id);
        if (item is null) return NotFound();
        return Ok(item);
    }

    // GET /api/orderitems/order/{orderId}
    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetByOrder(int orderId)
    {
        var items = await _db.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .ToListAsync();
        return Ok(items);
    }

    // POST /api/orderitems
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderItem item)
    {
        var orderExists = await _db.Orders.AnyAsync(o => o.Id == item.OrderId);
        if (!orderExists) return BadRequest("Order not found.");

        _db.OrderItems.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    // PATCH /api/orderitems/{id}
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrderItem updated)
    {
        var item = await _db.OrderItems.FindAsync(id);
        if (item is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(updated.ProductName)) item.ProductName = updated.ProductName;
        if (updated.Quantity > 0) item.Quantity = updated.Quantity;
        if (updated.UnitPrice > 0) item.UnitPrice = updated.UnitPrice;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    // DELETE /api/orderitems/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.OrderItems.FindAsync(id);
        if (item is null) return NotFound();

        _db.OrderItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

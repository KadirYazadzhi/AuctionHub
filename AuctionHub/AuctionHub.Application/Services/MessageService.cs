using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class MessageService : IMessageService
{
    private readonly IAuctionHubDbContext _context;

    public MessageService(IAuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ContactMessageDto>> GetAllAsync()
    {
        return await _context.ContactMessages
            .OrderByDescending(m => m.SentOn)
            .Select(m => new ContactMessageDto
            {
                Id = m.Id,
                Name = m.Name,
                Email = m.Email,
                Message = m.Message,
                SentOn = m.SentOn,
                IsRead = m.IsRead
            })
            .ToListAsync();
    }

    public async Task CreateAsync(ContactMessageDto model)
    {
        var message = new ContactMessage
        {
            Name = model.Name,
            Email = model.Email,
            Message = model.Message,
            SentOn = DateTime.UtcNow,
            IsRead = false
        };
        _context.ContactMessages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task MarkReadAsync(int id)
    {
        var message = await _context.ContactMessages.FindAsync(id);
        if (message != null)
        {
            message.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var message = await _context.ContactMessages.FindAsync(id);
        if (message != null)
        {
            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();
        }
    }
}

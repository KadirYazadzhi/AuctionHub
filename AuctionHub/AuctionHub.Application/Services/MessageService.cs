using AuctionHub.Application.DTOs;
using AuctionHub.Application.Interfaces;
using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class MessageService : IMessageService
{
    private readonly IAuctionHubDbContext _context;
    private readonly IEmailService _emailSender;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MessageService(
        IAuctionHubDbContext context, 
        IEmailService emailSender, 
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _emailSender = emailSender;
        _notificationService = notificationService;
        _userManager = userManager;
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

    public async Task<(bool Success, string Message)> ReplyAsync(int id, string replyContent)
    {
        var originalMessage = await _context.ContactMessages.FindAsync(id);
        if (originalMessage == null) return (false, "Message not found.");

        try
        {
            // 1. Send Email
            string emailBody = $@"
                <h3>Hello {originalMessage.Name},</h3>
                <p>Thank you for contacting AuctionHub. Here is our response to your inquiry:</p>
                <div style='background: #f8f9fa; padding: 15px; border-left: 4px solid #4361ee; margin: 20px 0;'>
                    {replyContent}
                </div>
                <hr/>
                <p style='color: #6c757d; font-size: 0.9em;'>Your original message:<br/>""{originalMessage.Message}""</p>
                <p>Best regards,<br/>The AuctionHub Team</p>";

            await _emailSender.SendEmailAsync(originalMessage.Email, "RE: AuctionHub Inquiry", emailBody);

            // 2. Check if user is registered to send In-App Notification
            var user = await _userManager.FindByEmailAsync(originalMessage.Email);
            if (user != null)
            {
                await _notificationService.NotifyUserAsync(user.Id, 
                    $"✉️ Admin Response: {replyContent}", 
                    "/Dashboard");
            }

            // 3. Mark as Read
            originalMessage.IsRead = true;
            await _context.SaveChangesAsync();

            return (true, "Reply sent successfully via email and notification.");
        }
        catch (Exception ex)
        {
            return (false, $"Error sending reply: {ex.Message}");
        }
    }
}

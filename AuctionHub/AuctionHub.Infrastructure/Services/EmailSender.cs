using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using SendGrid;
using CloudinaryDotNet;
using SendGrid.Helpers.Mail;

namespace AuctionHub.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public EmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var apiKey = _config["SendGrid:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            // Fallback for development if no key is provided
            Console.WriteLine("--- EMAIL SENT (No API Key) ---");
            Console.WriteLine($"To: {email}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Body: {htmlMessage}");
            return;
        }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress("no-reply@auctionhub.com", "AuctionHub Team");
        var to = new EmailAddress(email);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlMessage);

        await client.SendEmailAsync(msg);
    }
}

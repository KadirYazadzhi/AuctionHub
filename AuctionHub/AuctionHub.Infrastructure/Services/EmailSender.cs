using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;

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
        var host = _config["EmailSettings:Host"];
        var portStr = _config["EmailSettings:Port"];
        var username = _config["EmailSettings:Username"];
        var password = _config["EmailSettings:Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("--- SMTP LOG: Missing Credentials ---");
            Console.WriteLine($"To: {email}, Subject: {subject}");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("AuctionHub", username));
        message.To.Add(new MailboxAddress("", email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            // For Gmail or Mailtrap SMTP
            await client.ConnectAsync(host, int.Parse(portStr ?? "587"), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL SMTP FAILURE: {ex.Message}");
        }
    }
}

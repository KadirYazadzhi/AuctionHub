using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AuctionHub.Application.Interfaces;

namespace AuctionHub.Infrastructure.Services;

public class EmailSender : IEmailSender, IEmailService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailSender(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var apiToken = _config["EmailSettings:ApiToken"];
        var host = _config["EmailSettings:Host"];

        // Strategy 1: Mailtrap API (If Token is present)
        if (!string.IsNullOrEmpty(apiToken) && apiToken != "YOUR_MAILTRAP_TOKEN")
        {
            await SendViaMailtrapApiAsync(email, subject, htmlMessage, apiToken);
            return;
        }

        // Strategy 2: SMTP (If Host is present - Gmail, etc.)
        if (!string.IsNullOrEmpty(host) && host != "YOUR_SMTP_HOST")
        {
            await SendViaSmtpAsync(email, subject, htmlMessage);
            return;
        }

        // Fallback: Console Logging
        Console.WriteLine("--- EMAIL LOG (No Provider Configured) ---");
        Console.WriteLine($"To: {email}, Subject: {subject}");
    }

    private async Task SendViaMailtrapApiAsync(string email, string subject, string htmlMessage, string token)
    {
        const string inboxId = "4420755";
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var emailData = new {
            from = new { email = "hello@auctionhub.com", name = "AuctionHub" },
            to = new[] { new { email = email } },
            subject = subject,
            html = htmlMessage
        };

        var json = JsonSerializer.Serialize(emailData);
        
        // Retry logic for 429
        int delay = 2000;
        for (int i = 0; i < 3; i++)
        {
            var response = await client.PostAsync($"https://sandbox.api.mailtrap.io/api/send/{inboxId}", 
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode) return;
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                await Task.Delay(delay);
                delay *= 2;
                continue;
            }
            break;
        }
    }

    private async Task SendViaSmtpAsync(string email, string subject, string htmlMessage)
    {
        var host = _config["EmailSettings:Host"];
        var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
        var user = _config["EmailSettings:Username"];
        var pass = _config["EmailSettings:Password"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("AuctionHub", user));
        message.To.Add(new MailboxAddress("", email));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

        using var client = new SmtpClient();
        try {
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        } catch (Exception ex) {
            Console.WriteLine($"SMTP ERROR: {ex.Message}");
        }
    }
}

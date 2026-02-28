using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AuctionHub.Infrastructure.Services;

public class EmailSender : IEmailSender
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
        const string inboxId = "4420755"; // Your specific Mailtrap Sandbox Inbox ID
        
        if (string.IsNullOrEmpty(apiToken) || apiToken == "YOUR_MAILTRAP_TOKEN")
        {
            Console.WriteLine("--- MAILTRAP LOG (No Token) ---");
            Console.WriteLine($"To: {email}");
            Console.WriteLine($"Subject: {subject}");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var emailData = new
        {
            from = new { email = "hello@auctionhub.com", name = "AuctionHub" },
            to = new[] { new { email = email } },
            subject = subject,
            html = htmlMessage
        };

        var json = JsonSerializer.Serialize(emailData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // Using the Mailtrap Sandbox API URL with your Inbox ID
            var response = await client.PostAsync($"https://sandbox.api.mailtrap.io/api/send/{inboxId}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MAILTRAP SANDBOX ERROR: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EMAIL CRITICAL FAILURE: {ex.Message}");
        }
    }
}

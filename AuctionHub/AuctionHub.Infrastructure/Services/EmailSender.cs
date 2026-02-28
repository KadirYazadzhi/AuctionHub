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
        
        if (string.IsNullOrEmpty(apiToken) || apiToken == "YOUR_MAILTRAP_TOKEN")
        {
            // Fallback for local development - logs to console
            Console.WriteLine("--- MAILTRAP API LOG (No Token) ---");
            Console.WriteLine($"To: {email}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine("----------------------------------");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var emailData = new
        {
            from = new { email = "mailtrap@auctionhub.com", name = "AuctionHub Team" },
            to = new[] { new { email = email } },
            subject = subject,
            html = htmlMessage,
            category = "Identity Verification"
        };

        var json = JsonSerializer.Serialize(emailData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // Note: If using Sandbox, the URL is different. This is for the Sending API.
            // For Sandbox testing, use: https://sandbox.api.mailtrap.io/api/send/{inbox_id}
            // We'll use the universal Sending API URL here.
            var response = await client.PostAsync("https://send.api.mailtrap.io/api/send", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MAILTRAP ERROR: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL EMAIL FAILURE: {ex.Message}");
        }
    }
}

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
        const string inboxId = "4420755";
        
        if (string.IsNullOrEmpty(apiToken) || apiToken == "YOUR_MAILTRAP_TOKEN")
        {
            Console.WriteLine("--- MAILTRAP LOG (No Token) ---");
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
        
        int maxRetries = 3;
        int delaySeconds = 2;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"https://sandbox.api.mailtrap.io/api/send/{inboxId}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    return; // Success!
                }

                if (response.StatusCode == (System.Net.HttpStatusCode)429) // Too Many Requests
                {
                    Console.WriteLine($"MAILTRAP THROTTLED: Waiting {delaySeconds}s before retry {i+1}...");
                    await Task.Delay(delaySeconds * 1000);
                    delaySeconds *= 2; // Exponential backoff
                    continue;
                }

                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MAILTRAP SANDBOX ERROR: {response.StatusCode} - {error}");
                break; // Stop on other errors
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EMAIL ATTEMPT {i+1} FAILED: {ex.Message}");
                await Task.Delay(delaySeconds * 1000);
            }
        }
    }
}

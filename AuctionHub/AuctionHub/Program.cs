using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Infrastructure.ModelBinders;
using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using AuctionHub.Infrastructure.Services;
using System.Globalization;
using AuctionHub.Hubs;
using AuctionHub.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.SqlServer;
using AuctionHub.Infrastructure.Filters;
using DotNetEnv;
using Microsoft.Data.SqlClient;

// Load .env file for local development
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);

// Configure Global Culture for Euro
var cultureInfo = new CultureInfo("bg-BG");
cultureInfo.NumberFormat.CurrencySymbol = "€";
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Connection String Logic
var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? builder.Configuration["ConnectionStrings:DbServer"];
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? builder.Configuration["ConnectionStrings:DbName"];
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? builder.Configuration["ConnectionStrings:DbUser"];
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? builder.Configuration["ConnectionStrings:DbPassword"];

var connectionString = !string.IsNullOrEmpty(dbServer) && !string.IsNullOrEmpty(dbPassword)
    ? $"Server={dbServer};Database={dbName};User Id={dbUser};Password={dbPassword};Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true"
    : builder.Configuration.GetConnectionString("DefaultConnection") 
      ?? throw new InvalidOperationException("Connection string not found.");

// --- CRITICAL FIX: Ensure Database exists before Hangfire starts ---
var masterConnectionString = connectionString.Replace($"Database={dbName}", "Database=master");
using (var connection = new SqlConnection(masterConnectionString))
{
    connection.Open();
    using (var command = new SqlCommand($"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{dbName}') CREATE DATABASE [{dbName}]", connection))
    {
        command.ExecuteNonQuery();
    }
}

builder.Services.AddDbContext<AuctionHubDbContext>(options =>
    options.UseSqlServer(connectionString));

// Dependency Injection
builder.Services.AddScoped<IAuctionHubDbContext, AuctionHubDbContext>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBiddingNotificationService, SignalRBiddingNotificationService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddHttpClient<IImageAnalysisService, HuggingFaceImageAnalysisService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IEmailService, EmailSender>();
builder.Services.AddHttpClient();

// --- Hangfire Configuration ---
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

// Redis Configuration
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL")
    ?? builder.Configuration.GetConnectionString("Redis") 
    ?? "127.0.0.1:6379";

var redisConnectionString = $"{redisUrl},abortConnect=false";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "AuctionHub-DataProtection-Keys")
    .SetApplicationName("AuctionHub");

builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString, options => {
    options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("AuctionHub_SignalR");
});

builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = redisConnectionString;
    options.InstanceName = "AuctionHub_Cache_";
});

// Identity and External Authentication
builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false; // Changed to false for easier testing
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuctionHubDbContext>();

builder.Services.AddAuthentication()
    .AddGoogle(googleOptions => {
        googleOptions.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "placeholder";
        googleOptions.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "placeholder";
    })
    .AddGitHub(githubOptions => {
        githubOptions.ClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID") ?? "placeholder";
        githubOptions.ClientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET") ?? "placeholder";
    });

builder.Services.AddControllersWithViews(options => {
    options.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());
    options.ModelBinderProviders.Insert(0, new DoubleModelBinderProvider());
});
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("fixed", opt => { opt.PermitLimit = 10; opt.Window = TimeSpan.FromMinutes(1); });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AuctionHubDbContext>();
    
    // 1. Run Migrations
    await context.Database.MigrateAsync();
    
    // 2. Seed Data
    await DbSeeder.SeedAsync(services);

    // 3. Register Recurring Jobs
    var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<IAuctionService>("AuctionCleanup", service => service.CloseExpiredAuctionsAsync(), Cron.Minutely);
    recurringJobManager.AddOrUpdate<IAuctionService>("EscrowRelease", service => service.ReleaseEscrowFundsAsync(), Cron.Hourly);
    recurringJobManager.AddOrUpdate<IAuctionService>("DutchAuctionDrop", service => service.ProcessDutchAuctionsAsync(), Cron.Minutely);

    // 4. Identity Fixes
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@auctionhub.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser != null)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Administrator")) await roleManager.CreateAsync(new IdentityRole("Administrator"));
        if (!await userManager.IsInRoleAsync(adminUser, "Administrator")) await userManager.AddToRoleAsync(adminUser, "Administrator");
    }
}

app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<BiddingHub>("/hubs/bidding");
app.MapHub<ChatHub>("/hubs/chat");

await app.RunAsync();

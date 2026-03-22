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
using AuctionHub.Infrastructure.Filters;
using DotNetEnv;

// Load .env file for local development (K8s uses secrets, this is fallback)
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

// Add services to the container.
// Priority: Environment Variables > .env file > appsettings.json
var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? builder.Configuration["ConnectionStrings:DbServer"];
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? builder.Configuration["ConnectionStrings:DbName"];
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? builder.Configuration["ConnectionStrings:DbUser"];
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? builder.Configuration["ConnectionStrings:DbPassword"];

var connectionString = !string.IsNullOrEmpty(dbServer) && !string.IsNullOrEmpty(dbPassword)
    ? $"Server={dbServer};Database={dbName};User Id={dbUser};Password={dbPassword};Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true"
    : builder.Configuration.GetConnectionString("DefaultConnection") 
      ?? throw new InvalidOperationException("Connection string not found in environment variables or configuration.");

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
builder.Services.AddHttpClient<IImageAnalysisService, LocalAIImageAnalysisService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IEmailService, EmailSender>();
builder.Services.AddHttpClient();

// --- Hangfire Configuration ---
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

// Redis Configuration (Priority: Environment > appsettings)
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL")
    ?? builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";

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
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuctionHubDbContext>();

builder.Services.AddAuthentication()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") 
            ?? builder.Configuration["Authentication:Google:ClientId"] ?? "placeholder";
        googleOptions.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") 
            ?? builder.Configuration["Authentication:Google:ClientSecret"] ?? "placeholder";
    })
    .AddGitHub(githubOptions =>
    {
        githubOptions.ClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID") 
            ?? builder.Configuration["Authentication:GitHub:ClientId"] ?? "placeholder";
        githubOptions.ClientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET") 
            ?? builder.Configuration["Authentication:GitHub:ClientSecret"] ?? "placeholder";
    });

builder.Services.AddControllersWithViews(options => {
    options.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());
});
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => {
    options.HeaderName = "X-XSRF-TOKEN";
});

// Rate Limiting Configuration
builder.Services.AddRateLimiter(options =>
{
    // General rate limiter for most actions
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });
    
    // Strict rate limiter for critical bidding operations
    options.AddFixedWindowLimiter("bidding", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
    
    // Rate limiter for financial operations
    options.AddFixedWindowLimiter("financial", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 3;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter(); // Enable Rate Limiting

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Register Recurring Jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    // Correct way: use Type argument for the service
    recurringJobManager.AddOrUpdate<IAuctionService>("AuctionCleanup", service => service.CloseExpiredAuctionsAsync(), Cron.Minutely);
    recurringJobManager.AddOrUpdate<IAuctionService>("EscrowRelease", service => service.ReleaseEscrowFundsAsync(), Cron.Hourly);
    recurringJobManager.AddOrUpdate<IAuctionService>("DutchAuctionDrop", service => service.ProcessDutchAuctionsAsync(), Cron.Minutely);
}

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<BiddingHub>("/hubs/bidding");
app.MapHub<ChatHub>("/hubs/chat");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AuctionHubDbContext>();
    
    // Use Async Migration to prevent blocking the main thread
    await context.Database.MigrateAsync();
    
    // Fix users without UserNames or with Email as UserName (Optimized)
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var usersToFix = await userManager.Users
        .Where(u => string.IsNullOrEmpty(u.UserName) || u.UserName.Contains("@"))
        .ToListAsync();

    if (usersToFix.Any())
    {
        foreach (var user in usersToFix)
        {
            var source = !string.IsNullOrEmpty(user.UserName) ? user.UserName : user.Email;
            if (!string.IsNullOrEmpty(source) && source.Contains("@"))
            {
                var newUserName = source.Split('@')[0];
                if (!await userManager.Users.AnyAsync(u => u.UserName == newUserName))
                {
                    user.UserName = newUserName;
                    await userManager.UpdateNormalizedUserNameAsync(user);
                }
            }
        }
        await context.SaveChangesAsync();
    }

    await DbSeeder.SeedAsync(services);

    // Ensure Admin is Confirmed and has Role
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    string adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@auctionhub.com";
    string adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin123!";
    
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    
    if (adminUser != null)
    {
        // 1. Ensure Email is Confirmed
        if (!adminUser.EmailConfirmed)
        {
            adminUser.EmailConfirmed = true;
            await userManager.UpdateAsync(adminUser);
        }

        // 2. Ensure Role is Assigned
        if (!await roleManager.RoleExistsAsync("Administrator"))
        {
            await roleManager.CreateAsync(new IdentityRole("Administrator"));
        }
        
        if (!await userManager.IsInRoleAsync(adminUser, "Administrator"))
        {
            await userManager.AddToRoleAsync(adminUser, "Administrator");
        }
    }
}

await app.RunAsync();

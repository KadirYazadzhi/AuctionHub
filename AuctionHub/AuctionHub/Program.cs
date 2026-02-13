using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AuctionHub.Infrastructure.Data;
using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using AuctionHub.Application.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure Global Culture for Euro
var cultureInfo = new CultureInfo("bg-BG");
cultureInfo.NumberFormat.CurrencySymbol = "€";
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AuctionHubDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IAuctionHubDbContext, AuctionHubDbContext>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddHostedService<AuctionCleanupService>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 3;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuctionHubDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

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
                
                // Ensure the new username is not already taken
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
}

await app.RunAsync();

using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Briefed.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<BriefedDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<BriefedDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Register application services
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Ollama");
builder.Services.AddHttpClient("Groq");
builder.Services.AddHttpClient<INewsApiService, NewsApiService>();
builder.Services.AddScoped<IRssParserService, RssParserService>();
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddScoped<IGroqService, GroqService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<FeedUpdateService>();
builder.Services.AddScoped<OpmlImportService>();

// Add Hangfire for background jobs
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => 
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddHangfireServer();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Add Hangfire Dashboard (only in Development for security)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// Configure recurring job for feed updates
RecurringJob.AddOrUpdate<FeedUpdateService>(
    "update-all-feeds",
    service => service.UpdateAllFeedsAsync(),
    Cron.Hourly);

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Data.Context;
using TeknikServis.Data.Repositories;
using TeknikServis.Data.UnitOfWork;
using TeknikServis.Service.Services;
using TeknikServis.Web.Hubs;
using TeknikServis.Web.Identity;
using TeknikServis.Web.Services;


var builder = WebApplication.CreateBuilder(args);

// 1. SERVISLERÝN EKLENMESÝ
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// --- HANGFIRE KONFÝGÜRASYONU ---
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 3;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>()
.AddTokenProvider<EmailCodeTokenProvider>("EmailCode");

// SignalR
builder.Services.AddSignalR();

// Servisler
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ISmsService, IletiMerkeziSmsService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IServiceTicketService, ServiceTicketService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IEDevletService, EDevletService>();
builder.Services.AddScoped<TenantService>();

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Yetki Politikalarý
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CreatePolicy", policy => policy.RequireAssertion(c => c.User.IsInRole("Admin") || c.User.HasClaim("Permission", "Create")));
    options.AddPolicy("EditPolicy", policy => policy.RequireAssertion(c => c.User.IsInRole("Admin") || c.User.HasClaim("Permission", "Edit")));
    options.AddPolicy("DeletePolicy", policy => policy.RequireAssertion(c => c.User.IsInRole("Admin") || c.User.HasClaim("Permission", "Delete")));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
});

// Dil Ayarý
var supportedCultures = new[] { "tr-TR" };
var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

var app = builder.Build();

// 2. HTTP REQUEST PIPELINE
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization(localizationOptions);
//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Chat Rotasý
app.MapHub<SupportHub>("/supportHub");

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    IgnoreAntiforgeryToken = true
});

RecurringJob.AddOrUpdate<IBackupService>(
    "daily-database-backup",
    service => service.RunDailyBackupAsync(),
    Cron.Daily(3)
);

// Rota Tanýmlarý
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- UYGULAMA BAÞLANGIÇ ÝÞLEMLERÝ ---
using (var scope = app.Services.CreateScope())
{
    // 1. Rolleri Kontrol Et / Oluþtur
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var roles = new[] { "Admin", "Personel", "Deneme", "Technician" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    // 2. TÜM VERÝTABANLARINI GÜNCELLE (Multi-Tenant Migration)
    // Bu kýsým, her baþlatmada tüm þube veritabanlarýný gezerek yeni eklenen tablolarý veya güncellemeleri yansýtýr.
    try
    {
        var tenantService = scope.ServiceProvider.GetRequiredService<TenantService>();
        await tenantService.UpdateAllDatabasesAsync();
    }
    catch (Exception ex)
    {
        // Loglama yapýlabilir: Console.WriteLine("DB Güncelleme hatasý: " + ex.Message);
    }
}
app.MapControllers();

app.Run();
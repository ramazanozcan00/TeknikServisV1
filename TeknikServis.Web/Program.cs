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
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models; // Swagger için gerekli

var builder = WebApplication.CreateBuilder(args);

// 1. VERÝTABANI BAÐLANTISI
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.CommandTimeout(120); // 120 saniye
        }));

// 2. HANGFIRE KONFÝGÜRASYONU
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

// 3. IDENTITY (KULLANICI YÖNETÝMÝ)
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

// 4. SIGNALR
builder.Services.AddSignalR();

// 5. SERVÝSLER (DEPENDENCY INJECTION)
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
builder.Services.AddHttpClient();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

builder.Services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
{
    var config = builder.Configuration.GetSection("EvolutionApi");
    client.BaseAddress = new Uri(config["BaseUrl"]);
    client.DefaultRequestHeaders.Add("apikey", config["ApiKey"]);
});

// 6. JWT AUTHENTICATION AYARLARI
builder.Services.AddAuthentication(options =>
{
    // Varsayýlan þema Identity Cookie'dir, ancak API isteklerinde JWT devreye girer.
    // Buradaki ayar API için JWT'yi önceliklendirmeye yardýmcý olur.
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// 7. CONTROLLER VE SWAGGER
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer(); // Swagger için gerekli

// Swagger Konfigürasyonu (Authorize butonu için)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Teknik Servis API", Version = "v1" });

    // Swagger'da kilit simgesi çýkmasý ve Token girilebilmesi için:
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Lütfen token'ý 'Bearer {token}' formatýnda giriniz.",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// 8. YETKÝ POLÝTÝKALARI
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CreatePolicy", policy => policy.RequireAssertion(c => c.User.IsInRole("Admin") || c.User.HasClaim("Permission", "Create")));
    options.AddPolicy("EditPolicy", policy => policy.RequireAssertion(c => c.User.IsInRole("Admin") || c.User.HasClaim("Permission", "Edit")));
    options.AddPolicy("DeletePolicy", policy => policy.RequireAssertion(c => c.User.IsInRole("Admin") || c.User.HasClaim("Permission", "Delete")));
});

// 9. COOKIE AYARLARI (WEB MVC ÝÇÝN)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
});

// 10. DÝL AYARLARI
var supportedCultures = new[] { "tr-TR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

var app = builder.Build();

// --- PIPELINE (UYGULAMA AKIÞI) ---

// Swagger'ý Her Ortamda Aç (404 hatasýný çözer)
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Teknik Servis API v1"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization(localizationOptions);
app.UseStaticFiles();

app.UseRouting();

// Önce Authentication, Sonra Authorization
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

// Hangfire Job Tanýmý
RecurringJob.AddOrUpdate<IBackupService>(
    "daily-database-backup",
    service => service.RunDailyBackupAsync(),
    Cron.Daily(3)
);

// MVC Rota Tanýmlarý
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// API Controller Rotasý
app.MapControllers();

// --- UYGULAMA BAÞLANGIÇ ÝÞLEMLERÝ (SEED DATA & MIGRATION) ---
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
    try
    {
        var tenantService = scope.ServiceProvider.GetRequiredService<TenantService>();
        await tenantService.UpdateAllDatabasesAsync();
    }
    catch (Exception ex)
    {
        // Loglama yapýlabilir
        Console.WriteLine("DB Güncelleme hatasý: " + ex.Message);
    }
}

app.Run();
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
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. VERÝTABANI BAÐLANTISI
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.CommandTimeout(120);
        }));

// 2. HANGFIRE
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

// 3. IDENTITY
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

// 4. SIGNALR & HTTP CLIENT
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

// 5. SERVÝSLER
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
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

builder.Services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
{
    var config = builder.Configuration.GetSection("EvolutionApi");
    if (config.Exists())
    {
        client.BaseAddress = new Uri(config["BaseUrl"]);
        client.DefaultRequestHeaders.Add("apikey", config["ApiKey"]);
    }
});

// 6. KÝMLÝK DOÐRULAMA (DÜZELTÝLMÝÞ SON HAL)
builder.Services.AddAuthentication(options =>
{
    // Varsayýlan þema olarak "Akýllý Seçim"i ayarla
    options.DefaultScheme = "JWT_OR_COOKIE";
    options.DefaultChallengeScheme = "JWT_OR_COOKIE";
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
})
.AddPolicyScheme("JWT_OR_COOKIE", "JWT_OR_COOKIE", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // Gelen istekte "Bearer" token var mý?
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            return JwtBearerDefaults.AuthenticationScheme; // Varsa JWT kullan

        // Yoksa Identity'nin kendi Cookie sistemini kullan
        return IdentityConstants.ApplicationScheme;
    };
});

// 7. CONTROLLER & SWAGGER
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Teknik Servis API", Version = "v1" });
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
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

// 9. COOKIE AYARLARI
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

// --- PIPELINE ---

// Swagger'ý Her Ortamda Aç
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

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<SupportHub>("/supportHub");

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

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

// --- BAÞLANGIÇ ÝÞLEMLERÝ ---
using (var scope = app.Services.CreateScope())
{
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var roles = new[] { "Admin", "Personel", "Deneme", "Technician" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Rol hatasý: " + ex.Message);
    }

    try
    {
        var tenantService = scope.ServiceProvider.GetRequiredService<TenantService>();
        await tenantService.UpdateAllDatabasesAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB Güncelleme hatasý: " + ex.Message);
    }
}

app.Run();
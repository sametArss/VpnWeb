using BusiniessLayer.Abstract;
using BusiniessLayer.Concrete;
using BusiniessLayer.Security;
using DataAccessLayer.Concrete.Repository;
using DataAcsessLayer.Abstract;
using DataAcsessLayer.Concrete.Context;
using DataAcsessLayer.Concrete;
using DataAcsessLayer.EntityFramework;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore; // UseSqlServer için gerekli
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// 1. SERVİSLERİN EKLENMESİ
// -------------------------------------------------------------------------

builder.Services.AddControllersWithViews();

// --- Veritabanı Bağlantısı ---
// Not: UseSqlServer hatası devam ederse NuGet'ten 'Microsoft.EntityFrameworkCore.SqlServer' paketini yükle.
builder.Services.AddDbContext<VpnDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Dependency Injection (DI) Kayıtları ---

// Generic Repository Kaydı
builder.Services.AddScoped(typeof(IRepositoriesDal<>), typeof(GenericRepositoryDal<>));

// Data Access (DAL) Kayıtları
builder.Services.AddScoped<IUserDal, EFUserDal>();
builder.Services.AddScoped<IVpnServerDal, EFVpnServerDal>();
builder.Services.AddScoped<IUserVpnDal, EFUserVpnDal>();

// Business Layer Kaydı
// DİKKAT: .NET Identity ile çakışmaması için kendi UserManager sınıfını tam adıyla belirtiyoruz.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVpnServerService, VpnServerManager>();
builder.Services.AddScoped<IUserVpnService, UserVpnManager>();
builder.Services.AddScoped<IEmailService, MailManager>();


// Güvenlik Servisleri
builder.Services.AddScoped<JwtTokenService>();

// --- JWT Authentication Ayarları ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };

        // EKLENECEK KISIM BURASI 👇
        // Bu olay (Event), sisteme "Token'ı nerede arayayım?" sorusuna ek cevap verir.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Tarayıcıdaki çerezlerden 'access_token' isimli olanı al
                var accessToken = context.Request.Cookies["access_token"];

                // Eğer çerez varsa, sistemin token değişkenine bunu ata
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
        // EKLENECEK KISIM BİTTİ 👆
    });

// -------------------------------------------------------------------------
var app = builder.Build();
// -------------------------------------------------------------------------

// -------------------------------------------------------------------------
// 2. MIDDLEWARE SIRALAMASI
// -------------------------------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Sıralama kritik: Önce Authentication, sonra Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
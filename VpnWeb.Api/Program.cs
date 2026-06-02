using BusiniessLayer.Abstract;
using BusiniessLayer.Concrete;
using BusiniessLayer.Security;
using DataAccessLayer.Concrete.Repository;
using DataAcsessLayer.Abstract;
using DataAcsessLayer.Concrete.Context;
using DataAcsessLayer.EntityFramework;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SERVİSLERİN EKLENMESİ ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔥 BURASI KRİTİK (HATAYI ÇÖZER)
builder.Services.AddHttpContextAccessor();

// --- Veritabanı Bağlantısı ---
builder.Services.AddDbContext<VpnDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Dependency Injection (DI) Kayıtları ---
builder.Services.AddScoped(typeof(IRepositoriesDal<>), typeof(GenericRepositoryDal<>));
builder.Services.AddScoped<IUserDal, EFUserDal>();
builder.Services.AddScoped<IVpnServerDal, EFVpnServerDal>();
builder.Services.AddScoped<IUserVpnDal, EFUserVpnDal>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVpnServerService, VpnServerManager>();
builder.Services.AddScoped<IUserVpnService, UserVpnManager>();
builder.Services.AddScoped<IEmailService, MailManager>();
builder.Services.AddScoped<JwtTokenService>();

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// --- JWT AUTH ---
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
    });

var app = builder.Build();

// --- MIDDLEWARE ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
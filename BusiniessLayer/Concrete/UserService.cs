using BusiniessLayer.Abstract;
using BusiniessLayer.Security;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace BusiniessLayer.Concrete
{
    public class UserService : IUserService
    {
        private readonly IUserDal _userRepo;
        private readonly JwtTokenService _jwt;
        private readonly IEmailService _emailService;
        private readonly ILogger<UserService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(IUserDal userRepo, JwtTokenService jwt, IEmailService emailService, ILogger<UserService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _userRepo = userRepo;
            _jwt = jwt;
            _emailService = emailService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetIp() => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        public async Task RegisterAsync(RegisterDto dto)
        {
            var existingUser = await _userRepo.GetByFilterAsync(x => x.Email == dto.Email);

            PasswordHasher.Create(dto.Password, out var hash, out var salt);
            string verificationToken = Guid.NewGuid().ToString();

            // --- KULLANICI KONTROL MANTIĞI ---
            if (existingUser != null)
            {
                // Adam zaten doğrulanmışsa hata ver
                if (existingUser.IsEmailVerified)
                    throw new Exception("Bu e-posta adresi zaten kullanımda.");

                // Adam kayıt olmuş ama doğrulamamışsa (Token'ı yenile)
                existingUser.PasswordHash = hash;
                existingUser.PasswordSalt = salt;
                existingUser.FullName = dto.FullName;
                existingUser.EmailVerificationToken = verificationToken;
                existingUser.CreatedAt = DateTime.UtcNow;

                await _userRepo.UpdateAsync(existingUser);
            }
            else
            {
                // Yeni kayıt
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = dto.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FullName = dto.FullName,
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    IsEmailVerified = false,
                    EmailVerificationToken = verificationToken
                };
                await _userRepo.InsertAsync(user);
                _logger.LogInformation("[AUTH] Register success | Email={Email} IP={Ip}", dto.Email, GetIp());
            }
            // ----------------------------------

            // ⚠️ DÜZELTME BURADA: Controller ismin 'Account' olduğu için link 'Account' olmalı
            // İlerde port değişirse diye appsettings'den almak en iyisi ama şimdilik manuel:
            string verificationLink = $"https://localhost:7177/Account/VerifyEmail?token={verificationToken}";

            string emailBody = GetEmailTemplate(
                title: "E-posta Doğrulama",
                fullName: dto.FullName,
                message: "Global Shield 'a kayıt olduğunuz için teşekkür ederiz. Hesabınızı aktifleştirmek ve güvenli bağlantıları hemen kullanmaya başlamak için lütfen e-posta adresinizi doğrulayın.",
                buttonText: "Hesabımı Doğrula",
                buttonLink: verificationLink,
                footerMessage: "Yukarıdaki butona tıklayarak hesabınızı onaylayabilirsiniz."
            );

            await _emailService.SendEmailAsync(dto.Email, "Global Shield - E-posta Doğrulama", emailBody);
        }

        public async Task<string> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.GetByFilterAsync(x => x.Email == dto.Email);
            if (user == null)
            {
                _logger.LogWarning("[AUTH] Login failed - user not found | Email={Email} IP={Ip}", dto.Email, GetIp());
                throw new Exception("Kullanıcı bulunamadı.");
            }

            if (!PasswordHasher.Verify(dto.Password, user.PasswordHash, user.PasswordSalt))
            {
                _logger.LogWarning("[AUTH] Login failed - wrong password | Email={Email} IP={Ip}", dto.Email, GetIp());
                throw new Exception("Şifre hatalı.");
            }

            // KRİTİK KONTROL BURADA
            if (!user.IsEmailVerified)
            {
                _logger.LogWarning("[AUTH] Login failed - email not verified | Email={Email} IP={Ip}", dto.Email, GetIp());
                throw new Exception("Lütfen önce e-posta adresinize gelen linke tıklayarak hesabınızı doğrulayın.");
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user);

            _logger.LogInformation("[AUTH] Login success | UserId={UserId} Email={Email} IP={Ip}", user.Id, user.Email, GetIp());

            return _jwt.CreateToken(user.Id, user.Email, user.Role);
        }



        public async Task<User> GetUserByIdAsync(Guid id)
        {
            return await _userRepo.GetByIdAsync(id);
        }


        // Linke tıklandığında çalışacak metot
        public async Task VerifyEmailAsync(string token)
        {
            var user = await _userRepo.GetByFilterAsync(x => x.EmailVerificationToken == token);

            if (user == null)
                throw new Exception("Geçersiz veya süresi dolmuş doğrulama kodu.");

            // Hesabı doğrula
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null; // Token'ı sil ki tekrar kullanılamasın
            user.IsActive = true; // İstersen hesabı burada aktif edersin

            await _userRepo.UpdateAsync(user);
            _logger.LogInformation("[AUTH] Email verified | UserId={UserId} Email={Email} IP={Ip}", user.Id, user.Email, GetIp());
        }


        public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
        {
            // 1. Kullanıcıyı getir
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) throw new Exception("Kullanıcı bulunamadı.");

            // 2. Eski şifre doğru mu kontrol et
            if (!PasswordHasher.Verify(dto.OldPassword, user.PasswordHash, user.PasswordSalt))
            {
                throw new Exception("Mevcut şifreniz hatalı.");
            }

            // 3. Yeni şifreler uyuşuyor mu?
            if (dto.NewPassword != dto.ConfirmPassword)
            {
                throw new Exception("Yeni şifreler birbiriyle uyuşmuyor.");
            }

            // 4. Yeni şifrenin güvenliği (İstersen buraya karakter sayısı kontrolü ekleyebilirsin)
            if (dto.NewPassword.Length < 6)
            {
                throw new Exception("Yeni şifreniz en az 6 karakter olmalıdır.");
            }

            // 5. Yeni şifreyi Hashle
            PasswordHasher.Create(dto.NewPassword, out var newHash, out var newSalt);

            // 6. Bilgileri güncelle
            user.PasswordHash = newHash;
            user.PasswordSalt = newSalt;

            // 7. Veritabanına kaydet
            await _userRepo.UpdateAsync(user);
            _logger.LogInformation("[AUTH] Password changed | UserId={UserId} Email={Email} IP={Ip}", userId, user.Email, GetIp());
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var user = await _userRepo.GetByFilterAsync(x => x.Email == email);

            // Güvenlik için: kullanıcı yoksa bile hata fırlatma
            if (user == null) return;

            string token = Guid.NewGuid().ToString();

            user.PasswordResetToken = token;
            user.PasswordResetTokenExpire = DateTime.UtcNow.AddMinutes(15);

            await _userRepo.UpdateAsync(user);

            string link = $"https://localhost:7177/Account/ResetPassword?token={token}";

            string body = GetEmailTemplate(
                title: "Şifre Sıfırlama Talebi",
                fullName: user.FullName,
                message: "Hesabınızın parolasını sıfırlamak için bir talepte bulundunuz. Aşağıdaki butona tıklayarak yeni parolanızı güvenle belirleyebilirsiniz.",
                buttonText: "Şifremi Sıfırla",
                buttonLink: link,
                footerMessage: "Bu bağlantı güvenlik amacıyla 15 dakika süreyle geçerlidir."
            );

            await _emailService.SendEmailAsync(
                user.Email,
                "Global Shield - Şifre Sıfırlama Talebi",
                body
            );
        }

        public async Task ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userRepo.GetByFilterAsync(
                x => x.PasswordResetToken == dto.Token
            );

            if (user == null)
                throw new Exception("Geçersiz veya süresi dolmuş link.");

            if (user.PasswordResetTokenExpire < DateTime.UtcNow)
                throw new Exception("Link süresi dolmuş.");

            if (dto.NewPassword != dto.ConfirmPassword)
                throw new Exception("Şifreler uyuşmuyor.");

            PasswordHasher.Create(dto.NewPassword, out var hash, out var salt);

            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpire = null;

            await _userRepo.UpdateAsync(user);
            _logger.LogInformation("[AUTH] Password reset | UserId={UserId} Email={Email} IP={Ip}", user.Id, user.Email, GetIp());
        }

        private string GetEmailTemplate(string title, string fullName, string message, string buttonText, string buttonLink, string footerMessage)
        {
            return $@"
            <div style=""font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #0f172a; padding: 50px 20px; color: #e2e8f0; line-height: 1.5;"">
                <div style=""max-width: 600px; margin: 0 auto; background-color: #1e293b; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.5); border: 1px solid #334155;"">
                    
                    <div style=""background: linear-gradient(135deg, #1e3a8a, #3b82f6); padding: 40px 30px; text-align: center;"">
                        <div style=""font-size: 42px; margin-bottom: 15px;"">🛡️</div>
                        <h1 style=""color: #ffffff; margin: 0; font-size: 28px; font-weight: 800; letter-spacing: 2px; text-transform: uppercase;"">
                            Global<span style=""font-weight: 300; color: #bfdbfe;"">Shield</span>
                        </h1>
                        <p style=""color: #eff6ff; margin: 10px 0 0; font-size: 15px; font-weight: 500; letter-spacing: 1px; opacity: 0.9;"">{title}</p>
                    </div>
                    
                    <div style=""padding: 40px 30px; text-align: center;"">
                        <h2 style=""margin-top: 0; font-size: 22px; color: #f8fafc; font-weight: 600;"">Sayın {fullName},</h2>
                        <div style=""width: 40px; height: 3px; background-color: #3b82f6; margin: 20px auto; border-radius: 2px;""></div>
                        
                        <p style=""font-size: 16px; line-height: 1.7; color: #cbd5e1; margin-bottom: 35px;"">
                            {message}
                        </p>
                        
                        <div style=""margin-bottom: 35px;"">
                            <a href=""{buttonLink}"" style=""display: inline-block; background: linear-gradient(to right, #2563eb, #3b82f6); color: #ffffff; text-decoration: none; padding: 15px 35px; border-radius: 8px; font-weight: 600; font-size: 16px; box-shadow: 0 4px 15px rgba(59, 130, 246, 0.4); text-transform: uppercase; letter-spacing: 1px;"">
                                {buttonText}
                            </a>
                        </div>
                        
                        <p style=""font-size: 13px; color: #94a3b8; line-height: 1.6; margin: 0;"">
                            {footerMessage}
                        </p>
                    </div>
                    
                    <div style=""background-color: #0b0f19; border-top: 1px solid #1e293b; padding: 25px; text-align: center;"">
                        <p style=""margin: 0; font-size: 13px; color: #64748b; font-weight: 500;"">
                            © {DateTime.Now.Year} Global Shield VPN. Tüm hakları saklıdır.
                        </p>
                        <p style=""margin: 10px 0 0 0; font-size: 12px; color: #475569;"">
                            Eğer bu e-postayı siz talep etmediyseniz, lütfen dikkate almayınız ve hesabınızın güvende olduğundan emin olunuz.
                        </p>
                    </div>
                    
                </div>
            </div>";
        }

    }
}
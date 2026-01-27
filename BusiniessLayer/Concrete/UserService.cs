using BusiniessLayer.Abstract;
using BusiniessLayer.Security;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using System;
using System.Threading.Tasks;

namespace BusiniessLayer.Concrete
{
    public class UserService : IUserService
    {
        private readonly IUserDal _userRepo;
        private readonly JwtTokenService _jwt;
        private readonly IEmailService _emailService; // Mail servisini ekledik

        public UserService(IUserDal userRepo, JwtTokenService jwt, IEmailService emailService)
        {
            _userRepo = userRepo;
            _jwt = jwt;
            _emailService = emailService;
        }

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
            }
            // ----------------------------------

            // ⚠️ DÜZELTME BURADA: Controller ismin 'Account' olduğu için link 'Account' olmalı
            // İlerde port değişirse diye appsettings'den almak en iyisi ama şimdilik manuel:
            string verificationLink = $"https://localhost:7177/Account/VerifyEmail?token={verificationToken}";

            string emailBody = $"Merhaba {dto.FullName},<br>Hesabını doğrulamak için lütfen <a href='{verificationLink}'>buraya tıkla</a>.";

            await _emailService.SendEmailAsync(dto.Email, "GlobalShield - Hesap Doğrulama", emailBody);
        }

        public async Task<string> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.GetByFilterAsync(x => x.Email == dto.Email);
            if (user == null)
                throw new Exception("Kullanıcı bulunamadı.");

            if (!PasswordHasher.Verify(dto.Password, user.PasswordHash, user.PasswordSalt))
                throw new Exception("Şifre hatalı.");

            // KRİTİK KONTROL BURADA
            if (!user.IsEmailVerified)
                throw new Exception("Lütfen önce e-posta adresinize gelen linke tıklayarak hesabınızı doğrulayın.");

            user.LastLoginAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user);

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

            string body = $@"
        Merhaba {user.FullName},<br>
        Şifreni sıfırlamak için <a href='{link}'>buraya tıkla</a>.<br>
        Bu link 15 dakika geçerlidir.
    ";

            await _emailService.SendEmailAsync(
                user.Email,
                "GlobalShield - Şifre Sıfırlama",
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
        }


    }
}
using BusiniessLayer.Abstract;
using Microsoft.Extensions.Configuration; // Config okumak için
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BusiniessLayer.Concrete
{
    public class MailManager : IEmailService
    {
        private readonly IConfiguration _config;

        public MailManager(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            // 1. Ayarları appsettings.json'dan çekiyoruz
            var host = _config["EmailSettings:Host"];
            var port = int.Parse(_config["EmailSettings:Port"]);
            var fromEmail = _config["EmailSettings:User"];
            var password = _config["EmailSettings:Password"];
            var enableSsl = bool.Parse(_config["EmailSettings:EnableSsl"]);

            // 2. SMTP İstemcisini oluşturuyoruz (Postacı)
            using (var client = new SmtpClient(host, port))
            {
                client.Credentials = new NetworkCredential(fromEmail, password);
                client.EnableSsl = enableSsl;

                // 3. Mesajı hazırlıyoruz (Mektup)
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "VPN System Support"), // Gönderen görünen isim
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true // HTML kodları çalışsın diye (link vereceğiz ya hani)
                };

                mailMessage.To.Add(to); // Kime gidecek?

                // 4. Gönder!
                await client.SendMailAsync(mailMessage);
            }
        }
    }
}
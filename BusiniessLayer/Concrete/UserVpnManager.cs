using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using Renci.SshNet;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks; // Task kullanımı için şart
using System.IO; // MemoryStream için

namespace BusiniessLayer.Concrete
{
    public class UserVpnManager : IUserVpnService
    {
        private readonly IUserVpnDal _userVpnRepo;
        private readonly IVpnServerDal _vpnServerRepo;

        public UserVpnManager(IUserVpnDal userVpnRepo, IVpnServerDal vpnServerRepo)
        {
            _userVpnRepo = userVpnRepo;
            _vpnServerRepo = vpnServerRepo;
        }

        // ✅ ASYNC OLDU
        public async Task<bool> HasActiveVpnAsync(Guid userId)
        {
            var result = await _userVpnRepo.GetAllFilterAsync(x => x.UserId == userId && x.IsActive);
            return result.Any();
        }

        // ✅ ASYNC OLDU
        public async Task<UserVpn> GetActiveVpnAsync(Guid userId)
        {
            // .Result yerine await kullandık
            return await _userVpnRepo.GetByFilterAsync(
                x => x.UserId == userId && x.IsActive,
                x => x.VpnServer
            );
        }

        // ✅ ÖZEL METOD (Interface'de olmasına gerek yok, sadece burada kullanılıyor)
        private async Task<string> GetNextFreeIpAsync(int vpnServerId)
        {
            var existingVpns = await _userVpnRepo.GetAllFilterAsync(x => x.VpnServerId == vpnServerId);

            if (!existingVpns.Any())
            {
                return "10.0.0.2"; 
            }

            var maxIpOctet = existingVpns
                .Where(x => !string.IsNullOrEmpty(x.ClientIp)) // Null check ekledim garanti olsun
                .Select(x => int.Parse(x.ClientIp.Split('.')[3]))
                .Max();

            if (maxIpOctet >= 253) throw new Exception("IP Havuzu Doldu!");

            return $"10.0.0.{maxIpOctet + 1}";
        }

        // ✅ ASYNC BAĞLANTI METODU
        public async Task ConnectUserToVpnAsync(Guid userId, int vpnServerId)
        {
            // 1️⃣ Sunucuyu Çek
            var targetServer = await _vpnServerRepo.GetByIdAsync(vpnServerId);
            if (targetServer == null) throw new Exception("Sunucu bulunamadı!");

            // 2️⃣ IP ve İsim Belirle
            var clientName = $"user_{userId.ToString().Substring(0, 8)}";
            var clientIp = await GetNextFreeIpAsync(vpnServerId);

            // 3️⃣ Bağlantı Bilgileri
            var auth = new PasswordAuthenticationMethod(targetServer.SshUser, "123"); 

            var connection = new ConnectionInfo(
                targetServer.IpAddress, 
                targetServer.SshPort,   
                targetServer.SshUser,   
                auth
            );

            // 4️⃣ SSH İşlemleri
            using var ssh = new SshClient(connection);
            ssh.Connect(); // SshClient'ın Connect metodu genelde senkrondur, sorun yok.

            var command = $"sudo /etc/wireguard/add_peer.sh {clientName} {clientIp}";
            var result = ssh.RunCommand(command);

            if (result.ExitStatus != 0 && !result.Result.Contains("OK")) // ExitStatus kontrolü daha güvenli
                throw new Exception("VPN peer oluşturulamadı: " + result.Error);

            ssh.Disconnect();

            // 5️⃣ Config Dosyasını Çek
            using var sftp = new SftpClient(connection);
            sftp.Connect();

            using var ms = new MemoryStream();
            sftp.DownloadFile($"/home/{targetServer.SshUser}/{clientName}.conf", ms);

            var configText = Encoding.UTF8.GetString(ms.ToArray());
            sftp.Disconnect();

            // 6️⃣ DB Kaydı (Await ile)
            await _userVpnRepo.InsertAsync(new UserVpn
            {
                UserId = userId,
                VpnServerId = vpnServerId,
                ClientIp = clientIp,
                ConnectedAt = DateTime.UtcNow,
                IsActive = true,
                ClientConfig = configText
            });
        }

        // ✅ ASYNC DISCONNECT
        public async Task DisconnectAsync(Guid userId)
        {
            // 1. Aktif bağlantıyı SUNUCU BİLGİSİYLE birlikte bul
            var activeVpn = await _userVpnRepo.GetByFilterAsync(
                x => x.UserId == userId && x.IsActive, 
                x => x.VpnServer
            );

            if (activeVpn == null)
                throw new Exception("Aktif VPN bağlantısı bulunamadı.");

            var vpnServer = activeVpn.VpnServer ?? throw new Exception("VPN Server bilgisine erişilemedi!");

            // 2. SSH ile Sunucuya Bağlan
            var auth = new PasswordAuthenticationMethod(vpnServer.SshUser, "123"); 
            var connection = new ConnectionInfo(
                vpnServer.IpAddress,
                vpnServer.SshPort,
                vpnServer.SshUser,
                auth
            );

            using var ssh = new SshClient(connection);
            ssh.Connect();

            // 3. Kullanıcı adını belirle
            var clientName = $"user_{userId.ToString().Substring(0, 8)}";

            // 4. Peer Silme Komutunu Çalıştır
            // Eğer remove_peer.sh yoksa, 'wg set wg0 peer <PublicKey> remove' kullanılmalı.
            // Biz şimdilik add_peer.sh mantığından giderek remove_peer.sh olduğunu varsayıyoruz.
            var command = $"sudo /etc/wireguard/remove_peer.sh {clientName}";
            
            var result = ssh.RunCommand(command);

            if (!string.IsNullOrEmpty(result.Error) && !result.Result.Contains("OK"))
                 // Hata alsa bile veritabanından düşürmeli miyiz? Güvenlik için hayır, kullanıcı hala bağlı kalabilir.
                 throw new Exception("VPN peer silinemedi: " + result.Error);

            ssh.Disconnect();

            // 5. DB'de Pasife Çek
            activeVpn.IsActive = false;
            await _userVpnRepo.UpdateAsync(activeVpn);
        }
    }
}
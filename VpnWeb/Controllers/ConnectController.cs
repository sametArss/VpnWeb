using BusiniessLayer.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System;
using System.Threading.Tasks; // Task kullanımı için şart

namespace VpnWeb.Controllers
{
    [Authorize]
    public class ConnectController : Controller
    {
        private readonly IVpnServerService _vpnService;
        private readonly IUserVpnService _userVpnService;

        public ConnectController(
            IVpnServerService vpnService,
            IUserVpnService userVpnService)
        {
            _vpnService = vpnService;
            _userVpnService = userVpnService;
        }

        // Kanka sürekli aynı kodu yazmamak için bu property'i ekledim.
        // Artık 'CurrentUserId' diyerek ID'yi alabilirsin.
        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

        // ⚠️ Metot imzasını 'async Task<IActionResult>' olarak değiştirmeyi unutma!
        public async Task<IActionResult> Index()
        {
            // 1. Önce kullanıcının aktif bir VPN'i var mı diye soruyoruz.
            // Manager'da yazdığın HasActiveVpnAsync metodunu kullanıyoruz.
            bool hasActive = await _userVpnService.HasActiveVpnAsync(CurrentUserId);

            // 2. Eğer aktif bağlantı varsa, listeyi gösterme direkt Status'a postala.
            if (hasActive)
            {
                return RedirectToAction("Status");
            }

            // 3. Eğer yoksa normal akışa devam et, sunucuları listele.
            var vpns = _vpnService.GetActiveServers(); // Burası sync kalabilir veya async ise await eklersin.
            return View(vpns);
        }

        public async Task<IActionResult> Connect(int id)
        {
            try
            {
                // await kullanımı DOĞRU ✅
                await _userVpnService.ConnectUserToVpnAsync(CurrentUserId, id);
                return RedirectToAction("Status");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ⚠️ DÜZELTME 1: Metot async Task oldu
        public async Task<IActionResult> Status()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            // 1. Veriyi çek
            var activeVpn = await _userVpnService.GetActiveVpnAsync(userId);

            if (activeVpn == null)
            {
                ViewBag.Message = "Aktif bir VPN bağlantınız yok.";
                return View(null);
            }

            // 2. ⭐ QR KOD OLUŞTURMA BÖLÜMÜ ⭐
            // Eğer config string'i boş değilse QR üret
            if (!string.IsNullOrEmpty(activeVpn.ClientConfig))
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    // Config text'ini QR verisine çevir
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(activeVpn.ClientConfig, QRCodeGenerator.ECCLevel.Q);

                    // Linux uyumlu PNG oluşturucu (System.Drawing kullanmaz, her yerde çalışır)
                    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);

                    // Resmi byte dizisine çevir (20 piksel boyut çarpanı)
                    byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);

                    // HTML'de göstermek için Base64 formatına çevir
                    string base64String = Convert.ToBase64String(qrCodeAsPngByteArr);

                    // View'a taşı
                    ViewBag.QrCodeImage = "data:image/png;base64," + base64String;
                }
            }

            return View(activeVpn);
        }

        [HttpPost]
        public async Task<IActionResult> Disconnect()
        {
            try
            {
                // await kullanımı DOĞRU ✅
                await _userVpnService.DisconnectAsync(CurrentUserId);
                TempData["Success"] = "VPN bağlantısı kapatıldı";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Status");
        }

        [Authorize]
        // ⚠️ DÜZELTME 3: Metot async Task oldu
        public async Task<IActionResult> DownloadConfig()
        {
            // ⚠️ DÜZELTME 4: await eklendi.
            var vpn = await _userVpnService.GetActiveVpnAsync(CurrentUserId);

            // Eğer await koymasaydın, vpn.ClientConfig kısmında hata alırdın.
            if (vpn == null || string.IsNullOrEmpty(vpn.ClientConfig))
            {
                TempData["Error"] = "İndirilecek aktif VPN config bulunamadı";
                return RedirectToAction("Status");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(vpn.ClientConfig);
            var fileName = $"wg-{CurrentUserId.ToString().Substring(0, 6)}.conf";

            return File(
                bytes,
                "application/octet-stream",
                fileName
            );
        }
    }
}
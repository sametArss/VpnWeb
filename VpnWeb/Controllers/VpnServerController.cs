using BusiniessLayer.Abstract;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace VpnWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VpnServerController : Controller
    {
        private readonly IVpnServerService _vpnServerService;
        private readonly ILogger<VpnServerController> _logger;

        public VpnServerController(IVpnServerService vpnServerService, ILogger<VpnServerController> logger)
        {
            _vpnServerService = vpnServerService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var vpnServers = await _vpnServerService.GetActiveServersAsync();
                return View(vpnServers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching VPN servers");
                TempData["Error"] = "Sunucular yüklenirken bir hata oluştu.";
                return View(new List<VpnServer>());
            }
        }

        [HttpGet]
        public IActionResult Add() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(VpnServer vpnServer)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen formdaki tüm alanları doğru doldurun.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                vpnServer.CreatedAt = DateTime.UtcNow;
                vpnServer.IsActive = true;
                await _vpnServerService.AddVpnServerAsync(vpnServer);

                _logger.LogInformation("VPN server added: {Name}", vpnServer.Name);
                TempData["Success"] = $"{vpnServer.Name} başarıyla eklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding VPN server: {Name}", vpnServer.Name);
                TempData["Error"] = "Sunucu eklenirken bir hata oluştu.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0) return BadRequest();
            try
            {
                var vpnServer = await _vpnServerService.GetByIdAsync(id);
                if (vpnServer == null) return NotFound();
                return View(vpnServer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching VPN server: {Id}", id);
                TempData["Error"] = "Sunucu bilgileri yüklenirken bir hata oluştu.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(VpnServer vpnServer)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen formdaki tüm alanları doğru doldurun.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await _vpnServerService.UpdateVpnServerAsync(vpnServer);

                _logger.LogInformation("VPN server updated: {Id} - {Name}", vpnServer.Id, vpnServer.Name);
                TempData["Success"] = $"{vpnServer.Name} başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating VPN server: {Id}", vpnServer.Id);
                TempData["Error"] = "Sunucu güncellenirken bir hata oluştu.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest();
            try
            {
                await _vpnServerService.DeleteVpnServerAsync(id);

                _logger.LogInformation("VPN server deleted: {Id}", id);
                TempData["Success"] = "Sunucu başarıyla silindi.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting VPN server: {Id}", id);
                TempData["Error"] = "Sunucu silinirken bir hata oluştu.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetServerById(int id)
        {
            var server = await _vpnServerService.GetByIdAsync(id);
            if (server == null) return NotFound();
            return Json(server);
        }
    }
}
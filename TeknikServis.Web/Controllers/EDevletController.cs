using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Web.Services;

namespace TeknikServis.Web.Controllers
{
    // Giriş zorunluluğu olmasın derseniz [Authorize] kaldırabilirsiniz.
    [Authorize]
    public class EDevletController : Controller
    {
        private readonly IEDevletService _eDevletService;

        public EDevletController(IEDevletService eDevletService)
        {
            _eDevletService = eDevletService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Query(string imei)
        {
            if (string.IsNullOrEmpty(imei))
            {
                TempData["Error"] = "Lütfen bir IMEI numarası giriniz.";
                return RedirectToAction("Index");
            }

            var result = await _eDevletService.CheckImeiAsync(imei);

            // Sonucu View'a model olarak değil ViewBag ile taşıyalım (basitlik için)
            ViewBag.Result = result;

            return View("Index");
        }
    }
}
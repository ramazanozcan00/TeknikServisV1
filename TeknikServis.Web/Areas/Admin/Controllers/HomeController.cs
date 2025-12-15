using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Admin paneline girildiğinde varsayılan olarak Şube Listesine (veya istediğiniz başka bir sayfaya) yönlendirsin.
            // Admin alanında özel bir Dashboard sayfanız olmadığı için mevcut bir sayfaya yönlendiriyoruz.
            return RedirectToAction("Index", "Branch");
        }
    }
}
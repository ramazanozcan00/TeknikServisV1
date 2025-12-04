using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using System;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class AuditLogController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        // Sayfa numarası parametre olarak gelir (Varsayılan 1)
        public async Task<IActionResult> Index(int page = 1)
        {
            var branchId = User.GetBranchId();
            int pageSize = 15; // Her sayfada 15 kayıt

            // Servisten veriyi ve toplam sayıyı al
            var result = await _auditLogService.GetLogsByBranchAsync(branchId, page, pageSize);

            // Sayfalama bilgilerini View'a taşı
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.totalCount / pageSize);

            return View(result.logs);
        }
    }
}
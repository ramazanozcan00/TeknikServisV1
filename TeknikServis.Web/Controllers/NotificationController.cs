using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
    public class NotificationController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> GetAnnouncements()
        {
            // Sadece aktif olan son 5 duyuruyu getir
            var list = await _unitOfWork.Repository<Announcement>().GetAllAsync();
            var activeList = list
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedDate)
                .Take(5)
                .Select(x => new {
                    x.Title,
                    x.Content,
                    Date = x.CreatedDate.ToString("dd.MM HH:mm")
                })
                .ToList();

            return Json(activeList);
        }
    }
}
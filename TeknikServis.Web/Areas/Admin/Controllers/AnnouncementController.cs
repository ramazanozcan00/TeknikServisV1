using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AnnouncementController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public AnnouncementController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Listeleme
        public async Task<IActionResult> Index()
        {
            var list = await _unitOfWork.Repository<Announcement>().GetAllAsync();
            return View(list.OrderByDescending(x => x.CreatedDate));
        }

        // Ekleme Sayfası
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Ekleme İşlemi
        [HttpPost]
        public async Task<IActionResult> Create(Announcement model)
        {
            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid();
                model.CreatedDate = DateTime.Now;
                model.IsActive = true;

                await _unitOfWork.Repository<Announcement>().AddAsync(model);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Duyuru yayınlandı.";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // Silme İşlemi
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _unitOfWork.Repository<Announcement>().GetByIdAsync(id);
            if (item != null)
            {
                _unitOfWork.Repository<Announcement>().Remove(item);
                await _unitOfWork.CommitAsync();
            }
            return RedirectToAction("Index");
        }

        // Aktif/Pasif Yapma
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var item = await _unitOfWork.Repository<Announcement>().GetByIdAsync(id);
            if (item != null)
            {
                item.IsActive = !item.IsActive;
                _unitOfWork.Repository<Announcement>().Update(item);
                await _unitOfWork.CommitAsync();
            }
            return RedirectToAction("Index");
        }
    }
}
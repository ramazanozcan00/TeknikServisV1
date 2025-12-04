using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DefinitionController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public DefinitionController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Types = await _unitOfWork.Repository<DeviceType>().GetAllAsync();
            ViewBag.Brands = await _unitOfWork.Repository<DeviceBrand>().GetAllAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddType(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                await _unitOfWork.Repository<DeviceType>().AddAsync(new DeviceType { Name = name, Id = Guid.NewGuid(), CreatedDate = DateTime.Now });
                await _unitOfWork.CommitAsync();
                TempData["Success"] = "Cihaz türü eklendi.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddBrand(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                await _unitOfWork.Repository<DeviceBrand>().AddAsync(new DeviceBrand { Name = name, Id = Guid.NewGuid(), CreatedDate = DateTime.Now });
                await _unitOfWork.CommitAsync();
                TempData["Success"] = "Marka eklendi.";
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> DeleteType(Guid id)
        {
            var item = await _unitOfWork.Repository<DeviceType>().GetByIdAsync(id);
            if (item != null) { _unitOfWork.Repository<DeviceType>().Remove(item); await _unitOfWork.CommitAsync(); }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> DeleteBrand(Guid id)
        {
            var item = await _unitOfWork.Repository<DeviceBrand>().GetByIdAsync(id);
            if (item != null) { _unitOfWork.Repository<DeviceBrand>().Remove(item); await _unitOfWork.CommitAsync(); }
            return RedirectToAction("Index");
        }
    }
}
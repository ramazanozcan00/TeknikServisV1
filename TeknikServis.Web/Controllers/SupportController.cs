using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SupportController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // LİSTELEME
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            IEnumerable<SupportRequest> requests;

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                // Admin hepsini görür
                requests = await _unitOfWork.Repository<SupportRequest>().GetAllAsync();
            }
            else
            {
                // Kullanıcı sadece kendi açtıklarını görür
                requests = await _unitOfWork.Repository<SupportRequest>().FindAsync(x => x.UserId == user.Id);
            }

            return View(requests.OrderByDescending(x => x.CreatedDate));
        }

        // YENİ TALEP OLUŞTURMA (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // YENİ TALEP OLUŞTURMA (POST)
        [HttpPost]
        public async Task<IActionResult> Create(SupportRequest model, IFormFile attachment)
        {
            var user = await _userManager.GetUserAsync(User);

            // Dosya Yükleme
            if (attachment != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueName = Guid.NewGuid().ToString() + "_" + attachment.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }
                model.FilePath = "/uploads/" + uniqueName;
            }

            model.UserId = user.Id;
            model.CreatedDate = DateTime.Now;
            model.Id = Guid.NewGuid();

            await _unitOfWork.Repository<SupportRequest>().AddAsync(model);
            await _unitOfWork.CommitAsync();

            TempData["Success"] = "Destek talebiniz oluşturuldu.";
            return RedirectToAction("Index");
        }

        // DETAY VE CEVAPLAMA SAYFASI (GÜNCELLENDİ: OKUNDU İŞARETLEME EKLENDİ)
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            // User Include ederek çekiyoruz
            var request = (await _unitOfWork.Repository<SupportRequest>().FindAsync(x => x.Id == id, i => i.User)).FirstOrDefault();

            if (request == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);

            // Yetki Kontrolü (Başkası göremez, Admin hariç)
            if (!await _userManager.IsInRoleAsync(currentUser, "Admin") && request.UserId != currentUser.Id)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // --- OKUNDU İŞARETLEME (YENİ) ---
            // Kullanıcı kendi talebine bakıyorsa, cevaplanmışsa ve henüz görmediyse "Görüldü" yap.
            if (request.UserId == currentUser.Id && request.IsReplied && !request.IsSeen)
            {
                request.IsSeen = true;
                _unitOfWork.Repository<SupportRequest>().Update(request);
                await _unitOfWork.CommitAsync();
            }
            // --------------------------------

            return View(request);
        }

        // ADMİN CEVAPLAMA İŞLEMİ (POST)
        [HttpPost]
        [Authorize(Roles = "Admin")] // Sadece Admin
        public async Task<IActionResult> Reply(Guid id, string replyMessage)
        {
            var request = await _unitOfWork.Repository<SupportRequest>().GetByIdAsync(id);
            if (request != null)
            {
                request.AdminReply = replyMessage;
                request.IsReplied = true;
                request.IsSeen = false; // Yeni cevap geldiği için kullanıcı tekrar görmeli
                request.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<SupportRequest>().Update(request);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Cevap başarıyla gönderildi.";
            }
            return RedirectToAction("Details", new { id = id });
        }

        // MENÜ İÇİN BİLDİRİM KONTROLÜ (YENİ METOT)
        [HttpGet]
        public async Task<IActionResult> CheckNewReplies()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { count = 0 });

            // Kullanıcının açtığı, Adminin cevapladığı (IsReplied=true) ama henüz görülmeyen (IsSeen=false) kayıtlar
            var requests = await _unitOfWork.Repository<SupportRequest>()
                .FindAsync(x => x.UserId == user.Id && x.IsReplied && !x.IsSeen);

            return Json(new { count = requests.Count() });
        }
    }
}
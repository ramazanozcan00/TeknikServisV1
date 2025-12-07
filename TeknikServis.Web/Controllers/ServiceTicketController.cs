using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using TeknikServis.Web.Services;

namespace TeknikServis.Web.Controllers
{
    public class ServiceTicketController : Controller
    {
        private readonly IServiceTicketService _ticketService;
        private readonly ICustomerService _customerService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;

        // Constructor (Dependency Injection) - BURASI ORİJİNAL VE DOĞRU HALİDİR
        public ServiceTicketController(
            IServiceTicketService ticketService,
            ICustomerService customerService,
            IWebHostEnvironment webHostEnvironment,
            IEmailService emailService,
            IAuditLogService auditLogService,
            UserManager<AppUser> userManager,
            IUnitOfWork unitOfWork)
        {
            _ticketService = ticketService;
            _customerService = customerService;
            _webHostEnvironment = webHostEnvironment;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
        }

        // --- YARDIMCI METOT: Teknisyenleri Dropdown'a Doldurur ---
        private async Task LoadTechniciansAsync()
        {
            var technicians = await _userManager.GetUsersInRoleAsync("Technician");
            var techList = technicians.Select(u => new { Id = u.Id, Name = u.FullName }).ToList();
            ViewBag.Technicians = new SelectList(techList, "Id", "Name");
        }

        // --- 1. YEDEK PARÇA İŞLEMLERİ (AJAX) ---
        [HttpGet]
        public async Task<IActionResult> SearchSpareParts(string term)
        {
            Guid currentBranchId = User.GetBranchId(); // Şubeyi Al

            // Sadece bu şubenin stoklarını getir
            var parts = await _unitOfWork.Repository<SparePart>()
                .FindAsync(x => x.BranchId == currentBranchId);

            var result = parts
                .Where(p => p.ProductName.ToLower().Contains(term.ToLower()) && p.Quantity > 0)
                .Select(p => new {
                    id = p.Id,
                    label = $"{p.ProductName} (Stok: {p.Quantity} {p.UnitType}) - {p.SalesPrice:C2}",
                    price = p.SalesPrice
                })
                .Take(10)
                .ToList();

            return Json(result);
        }

        [HttpPost]
        [Authorize(Roles = "Technician,Admin")]
        public async Task<IActionResult> AddPartToTicket(Guid ticketId, Guid partId, int quantity)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticketId);
            var part = await _unitOfWork.Repository<SparePart>().GetByIdAsync(partId);

            if (ticket == null || part == null) return Json(new { success = false, message = "Kayıt bulunamadı." });
            if (part.Quantity < quantity) return Json(new { success = false, message = $"Yetersiz stok! Mevcut: {part.Quantity}" });

            // Stoktan Düş
            part.Quantity -= quantity;
            _unitOfWork.Repository<SparePart>().Update(part);

            // Servise Ekle
            var usedPart = new ServiceTicketPart
            {
                Id = Guid.NewGuid(),
                ServiceTicketId = ticketId,
                SparePartId = partId,
                Quantity = quantity,
                Price = part.SalesPrice,
                CreatedDate = DateTime.Now
            };
            await _unitOfWork.Repository<ServiceTicketPart>().AddAsync(usedPart);

            // Fiyatı Güncelle
            ticket.TotalPrice = (ticket.TotalPrice ?? 0) + (part.SalesPrice * quantity);
            ticket.UpdatedDate = DateTime.Now;
            _unitOfWork.Repository<ServiceTicket>().Update(ticket);

            await _unitOfWork.CommitAsync();
            return Json(new { success = true, message = "Parça eklendi." });
        }

        [HttpPost]
        [Authorize(Roles = "Technician,Admin")]
        public async Task<IActionResult> RemovePartFromTicket(Guid usedPartId)
        {
            var usedPart = await _unitOfWork.Repository<ServiceTicketPart>().GetByIdAsync(usedPartId);
            if (usedPart == null) return Json(new { success = false });

            // Stoğu İade Et
            var part = await _unitOfWork.Repository<SparePart>().GetByIdAsync(usedPart.SparePartId);
            if (part != null)
            {
                part.Quantity += usedPart.Quantity;
                _unitOfWork.Repository<SparePart>().Update(part);
            }

            // Fiyatı Düş
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(usedPart.ServiceTicketId);
            if (ticket != null)
            {
                ticket.TotalPrice -= usedPart.TotalPrice;
                if (ticket.TotalPrice < 0) ticket.TotalPrice = 0;
                _unitOfWork.Repository<ServiceTicket>().Update(ticket);
            }

            _unitOfWork.Repository<ServiceTicketPart>().Remove(usedPart);
            await _unitOfWork.CommitAsync();

            return Json(new { success = true, message = "Parça iptal edildi." });
        }

        // --- 2. TEKNİSYEN PANELİ VE İŞLEMLERİ ---
        [HttpGet]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> TechnicianPanel()
        {
            var user = await _userManager.GetUserAsync(User);

            // Aktif İşler
            var myTickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(t => t.TechnicianId == user.Id && t.Status != "Tamamlandı",
                           inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.DeviceType);

            // Tamamlanan İşler (ViewBag ile gönderiyoruz)
            var completed = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(t => t.TechnicianId == user.Id && t.Status == "Tamamlandı",
                           inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.DeviceType);
            ViewBag.CompletedTickets = completed;

            return View(myTickets);
        }

        [HttpGet]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> ProcessTicket(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            var ticket = await _ticketService.GetTicketByIdAsync(id);

            // Güvenlik Kontrolü
            if (ticket == null || ticket.TechnicianId != user.Id)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // Parça isimleri yüklenmediyse manuel yükle
            if (ticket.UsedParts != null)
            {
                foreach (var u in ticket.UsedParts)
                    if (u.SparePart == null) u.SparePart = await _unitOfWork.Repository<SparePart>().GetByIdAsync(u.SparePartId);
            }

            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> ProcessTicket(Guid id, string status, string description, decimal? price)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket != null)
            {
                string oldStatus = ticket.Status;
                ticket.Status = status;

                // Teknisyen Notu Ekleme
                if (!string.IsNullOrEmpty(description))
                {
                    string yeniNot = $"[{DateTime.Now:dd.MM.yyyy HH:mm}]: {description}";
                    if (string.IsNullOrEmpty(ticket.TechnicianNotes))
                        ticket.TechnicianNotes = yeniNot;
                    else
                        ticket.TechnicianNotes += "\n" + yeniNot;
                }

                if (price.HasValue) ticket.TotalPrice = price;
                ticket.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                await _unitOfWork.CommitAsync();

                // Loglama
                try
                {
                    string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    string userName = User.GetFullName();
                    Guid branchId = User.GetBranchId();
                    string userIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    string logDesc = $"Teknisyen İşlemi: Durum '{oldStatus}' -> '{status}' yapıldı.";

                    await _auditLogService.LogAsync(userId, userName, branchId, "Servis (Teknisyen)", "Güncelleme", logDesc, userIp);
                }
                catch { }

                TempData["Success"] = "İşlem kaydedildi.";
            }
            return RedirectToAction("TechnicianPanel");
        }

        // --- 3. STANDART CRUD İŞLEMLERİ ---

        // LİSTELEME
        [HttpGet]
        public async Task<IActionResult> Index(string s, string status, int page = 1)
        {
            Guid currentBranchId = User.GetBranchId();
            int pageSize = 10;

            var result = await _ticketService.GetAllTicketsByBranchAsync(currentBranchId, page, pageSize, s, status);

            ViewBag.Search = s;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.totalCount / pageSize);

            return View(result.tickets);
        }

        [HttpGet]
        [Authorize(Policy = "CreatePolicy")]
        public async Task<IActionResult> Create()
        {
            Guid currentBranchId = User.GetBranchId();

            var customers = await _customerService.GetCustomersByBranchAsync(currentBranchId);
            var customerList = customers.Select(c => new { Id = c.Id, DisplayText = $"{c.FirstName} {c.LastName} ({c.Phone})" });
            ViewBag.CustomerList = new SelectList(customerList, "Id", "DisplayText");

            var types = await _unitOfWork.Repository<DeviceType>().GetAllAsync();
            var brands = await _unitOfWork.Repository<DeviceBrand>().GetAllAsync();
            ViewBag.DeviceTypes = new SelectList(types, "Id", "Name");
            ViewBag.DeviceBrands = new SelectList(brands, "Id", "Name");

            await LoadTechniciansAsync();
            return View();
        }

        // CREATE (POST) - PDF PARAMETRESİ EKLENDİ
        [HttpPost]
        [Authorize(Policy = "CreatePolicy")]
        public async Task<IActionResult> Create(ServiceTicket ticket, IFormFile photo, IFormFile pdfFile)
        {
            // 1. HAK KONTROLÜ
            var user = await _userManager.GetUserAsync(User);
            if (await _userManager.IsInRoleAsync(user, "Deneme"))
            {
                if (user.TicketBalance <= 0)
                {
                    TempData["Error"] = "Deneme süresi: Servis kayıt hakkınız dolmuştur (0).";
                    return RedirectToAction("Index");
                }
                user.TicketBalance -= 1;
                await _userManager.UpdateAsync(user);
            }

            // 2. Fotoğraf Yükleme
            if (photo != null && photo.Length > 0)
            {
                string extension = Path.GetExtension(photo.FileName);
                string uniqueFileName = Guid.NewGuid().ToString() + extension;
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");

                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await photo.CopyToAsync(fileStream);
                }
                ticket.PhotoPath = "/uploads/" + uniqueFileName;
            }

            // 3. PDF Yükleme (YENİ)
            if (pdfFile != null && pdfFile.Length > 0)
            {
                string extension = Path.GetExtension(pdfFile.FileName);
                if (extension.ToLower() == ".pdf")
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + extension;
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "documents");

                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await pdfFile.CopyToAsync(fileStream);
                    }
                    ticket.PdfPath = "/documents/" + uniqueFileName;
                }
            }

            ticket.Id = Guid.NewGuid();
            await _ticketService.CreateTicketAsync(ticket);

            // --- E-POSTA BİLDİRİMİ ---
            try
            {
                var customer = await _customerService.GetByIdAsync(ticket.CustomerId);
                var brand = await _unitOfWork.Repository<DeviceBrand>().GetByIdAsync(ticket.DeviceBrandId);
                string brandName = brand != null ? brand.Name : "-";

                if (customer != null && !string.IsNullOrEmpty(customer.Email))
                {
                    string konu = $"Servis Kaydınız Alındı - Fiş No: {ticket.FisNo}";
                    string icerik = $@"
                    <div style='font-family: Segoe UI, Helvetica, Arial, sans-serif; padding: 20px; background-color: #f9f9f9; border: 1px solid #eee;'>
                        <div style='background-color: #ffffff; padding: 30px; border-radius: 10px; box-shadow: 0 2px 5px rgba(0,0,0,0.05);'>
                            <h2 style='color: #2563eb; margin-top: 0;'>Sayın {customer.FirstName} {customer.LastName},</h2>
                            <p style='font-size: 16px; color: #555;'>Cihazınız teknik servisimize kabul edilmiştir. İşlemler en kısa sürede başlayacaktır.</p>
                            <table style='width: 100%; border-collapse: collapse; margin-top: 20px; border: 1px solid #e0e0e0;'>
                                <tr style='background-color: #f1f5f9;'>
                                    <td style='padding: 12px; border-bottom: 1px solid #e0e0e0; width: 30%;'><strong>Fiş Numarası:</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #e0e0e0; font-size: 18px; font-weight: bold; color: #d63384;'>{ticket.FisNo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'><strong>Cihaz Bilgisi:</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'>{brandName} {ticket.DeviceModel}</td>
                                </tr>
                                <tr style='background-color: #f1f5f9;'>
                                    <td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'><strong>Arıza Tanımı:</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'>{ticket.ProblemDescription}</td>
                                </tr>
                            </table>
                            <div style='margin-top: 30px; text-align: center; color: #999; font-size: 12px;'>
                                <p>Bu e-posta otomatik olarak gönderilmiştir.</p>
                                <strong>Teknik Servis Takip Sistemi</strong>
                            </div>
                        </div>
                    </div>";

                    await _emailService.SendEmailAsync(customer.Email, konu, icerik);
                }
            }
            catch { }

            // Loglama
            try
            {
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                Guid branchId = User.GetBranchId();
                string userIp = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();

                await _auditLogService.LogAsync(userId, userName, branchId, "Servis", "Ekleme", $"Yeni kayıt açıldı: {ticket.FisNo}", userIp);
            }
            catch { }

            TempData["Success"] = "Kayıt açıldı.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpGet]
        [Authorize(Policy = "EditPolicy")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound();

            var types = await _unitOfWork.Repository<DeviceType>().GetAllAsync();
            var brands = await _unitOfWork.Repository<DeviceBrand>().GetAllAsync();
            ViewBag.DeviceTypes = new SelectList(types, "Id", "Name", ticket.DeviceTypeId);
            ViewBag.DeviceBrands = new SelectList(brands, "Id", "Name", ticket.DeviceBrandId);

            await LoadTechniciansAsync();
            return View(ticket);
        }

        // EDIT (POST) - TAMAMEN GÜNCELLENMİŞ HALİ
        [HttpPost]
        [Authorize(Policy = "EditPolicy")]
        public async Task<IActionResult> Edit(ServiceTicket ticket, IFormFile photo, IFormFile pdfFile)
        {
            // 1. Mevcut kaydı çek
            var existingTicket = await _ticketService.GetTicketByIdAsync(ticket.Id);

            if (existingTicket == null) return NotFound();

            // 2. Fotoğraf İşlemleri
            if (photo != null && photo.Length > 0)
            {
                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                using (var fileStream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create))
                {
                    await photo.CopyToAsync(fileStream);
                }
                existingTicket.PhotoPath = "/uploads/" + uniqueFileName;
            }

            // 3. PDF İşlemleri
            if (pdfFile != null && pdfFile.Length > 0)
            {
                string extension = Path.GetExtension(pdfFile.FileName);
                if (extension.ToLower() == ".pdf")
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + extension;
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "documents");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await pdfFile.CopyToAsync(fileStream);
                    }
                    existingTicket.PdfPath = "/documents/" + uniqueFileName;
                }
            }

            // 4. Standart Alanların Güncellenmesi
            existingTicket.DeviceTypeId = ticket.DeviceTypeId;
            existingTicket.DeviceBrandId = ticket.DeviceBrandId;
            existingTicket.DeviceModel = ticket.DeviceModel;
            existingTicket.SerialNumber = ticket.SerialNumber;
            existingTicket.ProblemDescription = ticket.ProblemDescription;
            existingTicket.IsWarranty = ticket.IsWarranty;

            // Yeni Alanlar
            existingTicket.InvoiceDate = ticket.InvoiceDate;
            existingTicket.Accessories = ticket.Accessories;
            existingTicket.PhysicalDamage = ticket.PhysicalDamage;

            // --- DEĞİŞİKLİK BURADA ---
            // Yönetici ise SADECE teknisyen atamasını güncellesin.
            // Notlara buradan müdahale edilmesin (Satır silindi).
            if (User.IsInRole("Admin"))
            {
                existingTicket.TechnicianId = ticket.TechnicianId;
                // existingTicket.TechnicianNotes = ticket.TechnicianNotes; // BU SATIR İPTAL EDİLDİ
            }

            existingTicket.UpdatedDate = DateTime.Now;

            // 5. Kaydet
            await _ticketService.UpdateTicketAsync(existingTicket);

            TempData["Success"] = "Kayıt başarıyla güncellendi.";
            return RedirectToAction("Details", new { id = ticket.Id });
        }
        [HttpPost]
        [Authorize(Policy = "EditPolicy")]
        public async Task<IActionResult> ChangeStatus(Guid id, string status, decimal? price)
        {
            await _ticketService.UpdateTicketStatusAsync(id, status, price);

            // Loglama
            try
            {
                var t = await _ticketService.GetTicketByIdAsync(id);
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                Guid branchId = User.GetBranchId();
                string userIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditLogService.LogAsync(userId, userName, branchId, "Servis", "Güncelleme", $"{t.FisNo} durumu '{status}' oldu.", userIp);

                if (status == "Tamamlandı" && t.Customer?.Email != null)
                {
                    await _emailService.SendEmailAsync(t.Customer.Email, "Cihaz Hazır", $"Fiş No: {t.FisNo} tamamlandı.");
                    TempData["Success"] = "Tamamlandı ve mail gönderildi.";
                }
                else
                {
                    TempData["Success"] = "Durum güncellendi.";
                }
            }
            catch { }

            return RedirectToAction("Details", new { id = id });
        }

        [HttpGet]
        public async Task<IActionResult> Print(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (await _userManager.IsInRoleAsync(user, "Deneme"))
            {
                if (user.PrintBalance <= 0) { TempData["Error"] = "Yazdırma hakkı bitti."; return RedirectToAction("Details", new { id = id }); }
                user.PrintBalance -= 1; await _userManager.UpdateAsync(user);
            }
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            return View(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> SendTicketPdfMail(Guid id, IFormFile pdfBlob)
        {
            var user = await _userManager.GetUserAsync(User);
            if (await _userManager.IsInRoleAsync(user, "Deneme"))
            {
                if (user.MailBalance <= 0) return Json(new { success = false, message = "Mail hakkı bitti." });
            }

            try
            {
                var ticket = await _ticketService.GetTicketByIdAsync(id);
                if (ticket?.Customer?.Email == null) return Json(new { success = false, message = "Mail bulunamadı." });
                if (pdfBlob == null || pdfBlob.Length == 0) return Json(new { success = false, message = "PDF yok." });

                byte[] fileBytes;
                using (var ms = new MemoryStream()) { await pdfBlob.CopyToAsync(ms); fileBytes = ms.ToArray(); }

                await _emailService.SendEmailWithAttachmentAsync(ticket.Customer.Email, $"Servis Fişi - {ticket.FisNo}", "Fişiniz ektedir.", fileBytes, "Fis.pdf");

                if (await _userManager.IsInRoleAsync(user, "Deneme")) { user.MailBalance -= 1; await _userManager.UpdateAsync(user); }

                return Json(new { success = true, message = "Mail gönderildi." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Hata: " + ex.Message }); }
        }

        // --- BARKOD İŞLEMLERİ ---
        [HttpGet]
        public IActionResult Scan() => View();

        [HttpGet]
        public async Task<IActionResult> FindTicketByFisNo(string fisNo)
        {
            if (string.IsNullOrWhiteSpace(fisNo)) return Json(new { success = false, message = "Barkod okunamadı." });

            var ticket = await _ticketService.GetTicketByFisNoAsync(fisNo.Trim());
            if (ticket == null) return Json(new { success = false, message = "Kayıt bulunamadı." });

            if (!User.IsInRole("Admin") && ticket.Customer.BranchId != User.GetBranchId())
                return Json(new { success = false, message = "Yetkiniz yok." });

            return Json(new { success = true, id = ticket.Id });
        }
    }
}
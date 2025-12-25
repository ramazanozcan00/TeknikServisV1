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
        private readonly IWhatsAppService _whatsAppService;
        private readonly IConfiguration _configuration;

        public ServiceTicketController(
            IServiceTicketService ticketService,
            ICustomerService customerService,
            IWebHostEnvironment webHostEnvironment,
            IEmailService emailService,
            IAuditLogService auditLogService,
            UserManager<AppUser> userManager,
            IUnitOfWork unitOfWork,
            IWhatsAppService whatsAppService,
            IConfiguration configuration)
        {
            _ticketService = ticketService;
            _customerService = customerService;
            _webHostEnvironment = webHostEnvironment;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _whatsAppService = whatsAppService;
            _configuration = configuration;
        }

        private async Task LoadTechniciansAsync()
        {
            Guid currentBranchId = User.GetBranchId();
            var technicians = await _userManager.GetUsersInRoleAsync("Technician");
            var techList = technicians
                .Where(u => u.BranchId == currentBranchId)
                .Select(u => new { Id = u.Id, Name = u.FullName })
                .ToList();
            ViewBag.Technicians = new SelectList(techList, "Id", "Name");
        }

        [HttpGet]
        public async Task<IActionResult> SearchSpareParts(string term)
        {
            Guid currentBranchId = User.GetBranchId();
            var parts = await _unitOfWork.Repository<SparePart>()
                .FindAsync(x => x.BranchId == currentBranchId);

            var result = parts
                .Where(p => p.ProductName.ToLower().Contains(term.ToLower()) && p.Quantity > 0)
                .Select(p => new
                {
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

            part.Quantity -= quantity;
            _unitOfWork.Repository<SparePart>().Update(part);

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

            var part = await _unitOfWork.Repository<SparePart>().GetByIdAsync(usedPart.SparePartId);
            if (part != null)
            {
                part.Quantity += usedPart.Quantity;
                _unitOfWork.Repository<SparePart>().Update(part);
            }

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

        [HttpGet]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> TechnicianPanel()
        {
            var user = await _userManager.GetUserAsync(User);
            var myTickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(t => t.TechnicianId == user.Id &&
                           t.Status != "Tamamlandı" &&
                           t.Status != "Onarım Tamamlandı" &&
                           t.Status != "Ödeme Yapıldı" &&
                           t.Status != "İptal",
                           inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.DeviceType);

            var completed = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(t => t.TechnicianId == user.Id &&
                           (t.Status == "Tamamlandı" || t.Status == "Onarım Tamamlandı" || t.Status == "Ödeme Yapıldı"),
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

            if (ticket == null || ticket.TechnicianId != user.Id)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (ticket.UsedParts != null)
            {
                foreach (var u in ticket.UsedParts)
                    if (u.SparePart == null) u.SparePart = await _unitOfWork.Repository<SparePart>().GetByIdAsync(u.SparePartId);
            }

            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> ProcessTicket(Guid id, string technicianStatus, string description, decimal? price)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket == null) return NotFound();

            var allowedStatuses = new[] {
                "İşlemde", "Parça Bekliyor",
                "Onarım Fiyat Bilgisi Verildi",
                "Onarım Sürüyor", "Onarım Tamamlandı"
            };

            if (!allowedStatuses.Contains(technicianStatus))
            {
                TempData["Error"] = "Bu durum için yetkiniz yok.";
                return RedirectToAction("TechnicianPanel");
            }

            ticket.TechnicianStatus = technicianStatus;

            if (price.HasValue) ticket.TotalPrice = price;

            if (!string.IsNullOrEmpty(description))
            {
                string yeniNot = $"[{DateTime.Now:dd.MM.yyyy HH:mm}]: {description}";
                ticket.TechnicianNotes = string.IsNullOrEmpty(ticket.TechnicianNotes) ? yeniNot : ticket.TechnicianNotes + "\n" + yeniNot;
            }

            _unitOfWork.Repository<ServiceTicket>().Update(ticket);
            await _unitOfWork.CommitAsync();

            TempData["Success"] = "İşlem kaydedildi.";
            return RedirectToAction("TechnicianPanel");
        }

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
            var customerList = customers.Select(c => new
            {
                Id = c.Id,
                DisplayText = string.IsNullOrEmpty(c.CompanyName)
                      ? $"{c.FirstName} {c.LastName} ({c.Phone})"
                      : $"{c.FirstName} {c.LastName} - {c.CompanyName} ({c.Phone})"
            });
            ViewBag.CustomerList = new SelectList(customerList, "Id", "DisplayText");
            var types = await _unitOfWork.Repository<DeviceType>().GetAllAsync();
            var brands = await _unitOfWork.Repository<DeviceBrand>().GetAllAsync();
            ViewBag.DeviceTypes = new SelectList(types, "Id", "Name");
            ViewBag.DeviceBrands = new SelectList(brands, "Id", "Name");
            await LoadTechniciansAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = "CreatePolicy")]
        public async Task<IActionResult> Create(ServiceTicket ticket, IFormFile photo, IFormFile pdfFile)
        {
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

            if (photo != null && photo.Length > 0)
            {
                string extension = Path.GetExtension(photo.FileName);
                string uniqueFileName = Guid.NewGuid().ToString() + extension;
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await photo.CopyToAsync(fileStream); }
                ticket.PhotoPath = "/uploads/" + uniqueFileName;
            }

            if (pdfFile != null && pdfFile.Length > 0)
            {
                string extension = Path.GetExtension(pdfFile.FileName);
                if (extension.ToLower() == ".pdf")
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + extension;
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "documents");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create)) { await pdfFile.CopyToAsync(fileStream); }
                    ticket.PdfPath = "/documents/" + uniqueFileName;
                }
            }

            ticket.Id = Guid.NewGuid();
            await _ticketService.CreateTicketAsync(ticket);

            try
            {
                var customer = await _customerService.GetByIdAsync(ticket.CustomerId);
                var brand = await _unitOfWork.Repository<DeviceBrand>().GetByIdAsync(ticket.DeviceBrandId);
                string brandName = brand != null ? brand.Name : "-";
                if (customer != null && !string.IsNullOrEmpty(customer.Email))
                {
                    string konu = $"Servis Kaydınız Alındı - Fiş No: {ticket.FisNo}";
                    string icerik = $@"<div style='font-family: Segoe UI, Helvetica, Arial, sans-serif; padding: 20px; background-color: #f9f9f9; border: 1px solid #eee;'><div style='background-color: #ffffff; padding: 30px; border-radius: 10px; box-shadow: 0 2px 5px rgba(0,0,0,0.05);'><h2 style='color: #2563eb; margin-top: 0;'>Sayın {customer.FirstName} {customer.LastName},</h2><p style='font-size: 16px; color: #555;'>Cihazınız teknik servisimize kabul edilmiştir.</p><table style='width: 100%; border-collapse: collapse; margin-top: 20px; border: 1px solid #e0e0e0;'><tr style='background-color: #f1f5f9;'><td style='padding: 12px; border-bottom: 1px solid #e0e0e0; width: 30%;'><strong>Fiş Numarası:</strong></td><td style='padding: 12px; border-bottom: 1px solid #e0e0e0; font-size: 18px; font-weight: bold; color: #d63384;'>{ticket.FisNo}</td></tr><tr><td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'><strong>Cihaz Bilgisi:</strong></td><td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'>{brandName} {ticket.DeviceModel}</td></tr><tr style='background-color: #f1f5f9;'><td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'><strong>Arıza Tanımı:</strong></td><td style='padding: 12px; border-bottom: 1px solid #e0e0e0;'>{ticket.ProblemDescription}</td></tr></table></div></div>";
                    await _emailService.SendEmailAsync(customer.Email, konu, icerik);
                }
            }
            catch { }

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

        [HttpPost]
        [Authorize(Policy = "EditPolicy")]
        public async Task<IActionResult> Edit(ServiceTicket ticket, IFormFile photo, IFormFile pdfFile)
        {
            var existingTicket = await _ticketService.GetTicketByIdAsync(ticket.Id);
            if (existingTicket == null) return NotFound();

            if (photo != null && photo.Length > 0)
            {
                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                using (var fileStream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create)) { await photo.CopyToAsync(fileStream); }
                existingTicket.PhotoPath = "/uploads/" + uniqueFileName;
            }

            if (pdfFile != null && pdfFile.Length > 0)
            {
                string extension = Path.GetExtension(pdfFile.FileName);
                if (extension.ToLower() == ".pdf")
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + extension;
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "documents");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create)) { await pdfFile.CopyToAsync(fileStream); }
                    existingTicket.PdfPath = "/documents/" + uniqueFileName;
                }
            }

            existingTicket.DeviceTypeId = ticket.DeviceTypeId;
            existingTicket.DeviceBrandId = ticket.DeviceBrandId;
            existingTicket.DeviceModel = ticket.DeviceModel;
            existingTicket.SerialNumber = ticket.SerialNumber;
            existingTicket.ProblemDescription = ticket.ProblemDescription;
            existingTicket.IsWarranty = ticket.IsWarranty;
            existingTicket.InvoiceDate = ticket.InvoiceDate;
            existingTicket.Accessories = ticket.Accessories;
            existingTicket.PhysicalDamage = ticket.PhysicalDamage;

            if (User.IsInRole("Admin")) existingTicket.TechnicianId = ticket.TechnicianId;
            existingTicket.UpdatedDate = DateTime.Now;

            await _ticketService.UpdateTicketAsync(existingTicket);
            TempData["Success"] = "Kayıt başarıyla güncellendi.";
            return RedirectToAction("Details", new { id = ticket.Id });
        }

        [HttpPost]
        [Authorize(Policy = "EditPolicy")]
        public async Task<IActionResult> ChangeStatus(Guid id, string status, string price)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = status;

            if (!string.IsNullOrEmpty(price))
            {
                string cleanPrice = price.Replace("₺", "").Replace("TL", "").Trim();
                if (decimal.TryParse(cleanPrice, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("tr-TR"), out decimal parsedPrice))
                {
                    ticket.TotalPrice = parsedPrice;
                }
                else if (decimal.TryParse(cleanPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsedPriceInvariant))
                {
                    ticket.TotalPrice = parsedPriceInvariant;
                }
            }

            ticket.UpdatedDate = DateTime.Now;
            _unitOfWork.Repository<ServiceTicket>().Update(ticket);
            await _unitOfWork.CommitAsync();

            try
            {
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                Guid branchId = User.GetBranchId();
                string userIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                await _auditLogService.LogAsync(userId, userName, branchId, "Servis", "Güncelleme", $"{ticket.FisNo} durumu '{status}' olarak güncellendi.", userIp);

                if (status == "Tamamlandı" && ticket.Customer?.Email != null)
                {
                    await _emailService.SendEmailAsync(ticket.Customer.Email, "Cihaz Hazır", $"Fiş No: {ticket.FisNo} işlemleri tamamlandı.");
                    TempData["Success"] = "Tamamlandı ve mail gönderildi.";
                }
                else
                {
                    TempData["Success"] = "Durum ve ücret güncellendi.";
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
            if (await _userManager.IsInRoleAsync(user, "Deneme")) { if (user.MailBalance <= 0) return Json(new { success = false, message = "Mail hakkı bitti." }); }
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

        [HttpGet]
        public IActionResult Scan() => View();

        [HttpGet]
        public async Task<IActionResult> FindTicketByFisNo(string fisNo)
        {
            if (string.IsNullOrWhiteSpace(fisNo)) return Json(new { success = false, message = "Barkod okunamadı." });
            var ticket = await _ticketService.GetTicketByFisNoAsync(fisNo.Trim());
            if (ticket == null) return Json(new { success = false, message = "Kayıt bulunamadı." });
            if (!User.IsInRole("Admin") && ticket.Customer.BranchId != User.GetBranchId()) return Json(new { success = false, message = "Yetkiniz yok." });
            return Json(new { success = true, id = ticket.Id });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.IsInRole("Admin") && !User.HasClaim(x => x.Type == "Permission" && x.Value == "Delete")) { TempData["Error"] = "Silme yetkiniz bulunmamaktadır."; return RedirectToAction("Index"); }
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null) { TempData["Error"] = "Kayıt bulunamadı."; return RedirectToAction("Index"); }
            if (!User.IsInRole("Admin") && ticket.Customer.BranchId != User.GetBranchId()) { TempData["Error"] = "Başka şubenin kaydını silemezsiniz."; return RedirectToAction("Index"); }
            await _ticketService.DeleteTicketAsync(id);
            try
            {
                string userId = _userManager.GetUserId(User);
                string userName = User.GetFullName();
                Guid branchId = User.GetBranchId();
                await _auditLogService.LogAsync(userId, userName, branchId, "Servis", "Silme", $"{ticket.FisNo} nolu kayıt silindi.", HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            catch { }
            TempData["Success"] = "Servis kaydı başarıyla silindi.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Personnel,Admin")]
        public async Task<IActionResult> ApproveToAccount(Guid ticketId, string finalAmount, string paymentType)
        {
            if (!decimal.TryParse(finalAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                return Json(new { success = false, message = "Geçersiz tutar formatı." });

            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticketId);
            if (ticket == null) return Json(new { success = false, message = "Kayıt bulunamadı." });

            ticket.Status = "Tamamlandı";
            ticket.TotalPrice = amount;
            _unitOfWork.Repository<ServiceTicket>().Update(ticket);

            var movement = new CustomerMovement
            {
                Id = Guid.NewGuid(),
                CustomerId = ticket.CustomerId,
                ServiceTicketId = ticket.Id,
                Amount = amount,
                MovementType = paymentType == "Borç" ? "Borç" : "Alacak",
                Description = $"{ticket.FisNo} No'lu servis bedeli. Tip: {paymentType}",
                BranchId = User.GetBranchId(),
                CreatedDate = DateTime.Now
            };

            await _unitOfWork.Repository<CustomerMovement>().AddAsync(movement);
            await _unitOfWork.CommitAsync();

            return Json(new { success = true, message = "Cariye başarıyla aktarıldı." });
        }

        [HttpPost]
        public async Task<IActionResult> GetTicketContactInfo(Guid id)
        {
            // 1. Servis Fişini Çek
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket == null) return Json(new { success = false, message = "Servis kaydı bulunamadı." });

            // 2. Müşteriyi Çek (Taze veri)
            var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(ticket.CustomerId);
            if (customer == null) return Json(new { success = false, message = "Müşteri kartı bulunamadı." });

            // 3. Varsayılan: Müşterinin kartındaki 2. numara (Eski numara burada olabilir)
            string phone2ToUse = customer.Phone2;

            // 4. ŞİRKET EŞLEŞTİRME (EN GÜNCEL VE HATASIZ EŞLEŞME)
            if (!string.IsNullOrWhiteSpace(customer.CompanyName))
            {
                // Müşterideki ismin tüm boşluklarını sil ve küçük harfe çevir (Örn: "Özdemir  İnşaat" -> "özdemirinşaat")
                string arananFirma = customer.CompanyName.ToLower().Replace(" ", "").Trim();

                // Veritabanındaki tüm şirket ayarlarını çek
                var tumFirmalar = await _unitOfWork.Repository<CompanySetting>().GetAllAsync();

                // Bellekte filtreleme yap:
                // 1. İsmi normalize ederek eşleştir.
                // 2. OrderByDescending ile EN SON GÜNCELLENEN (veya eklenen) kaydı en başa al.
                var eslesenFirma = tumFirmalar
                    .Where(x => !string.IsNullOrEmpty(x.CompanyName))
                    .Where(x => x.CompanyName.ToLower().Replace(" ", "").Trim() == arananFirma)
                    .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate) // <--- KRİTİK NOKTA: En yeni kaydı seç
                    .FirstOrDefault();

                // Eğer eşleşen bir firma bulunduysa ve numarasında veri varsa, KESİNLİKLE bunu kullan
                if (eslesenFirma != null && !string.IsNullOrEmpty(eslesenFirma.Phone))
                {
                    phone2ToUse = eslesenFirma.Phone;
                }
            }

            return Json(new
            {
                success = true,
                phone1 = customer.Phone,
                phone2 = phone2ToUse, // Artık en güncel firma numarası buradadır
                companyName = customer.CompanyName,
                isCorporate = !string.IsNullOrEmpty(customer.CompanyName)
            });
        }

        // --- GÜNCELLENEN METOD 1: Detaylı Bilgi Gönder ---
        [HttpPost]
        [Authorize(Roles = "Technician,Admin,Personnel")]
        public async Task<IActionResult> SendDetailedInfoMessage(Guid id, string targetPhone = null)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null && !currentUser.IsWhatsAppEnabled)
                    return Json(new { success = false, message = "WhatsApp yetkiniz yok." });

                var ticket = await _unitOfWork.Repository<ServiceTicket>()
                    .GetByIdWithIncludesAsync(x => x.Id == id, inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.UsedParts);

                if (ticket == null) return Json(new { success = false, message = "Kayıt bulunamadı." });

                // HEDEF NUMARA KONTROLÜ
                string gonderilecekNo = !string.IsNullOrEmpty(targetPhone) ? targetPhone : ticket.Customer.Phone;

                if (string.IsNullOrEmpty(gonderilecekNo))
                    return Json(new { success = false, message = "Geçerli bir telefon numarası bulunamadı." });

                // Mesaj İçeriği
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"Sayın *{ticket.Customer.FirstName} {ticket.Customer.LastName}*,");
                if (!string.IsNullOrEmpty(ticket.Customer.CompanyName)) sb.AppendLine($"({ticket.Customer.CompanyName})");

                sb.AppendLine($"*{ticket.FisNo}* fiş numaralı cihazınızın ({ticket.DeviceBrand?.Name} {ticket.DeviceModel}) işlemleri hakkında detaylar aşağıdadır:");
                sb.AppendLine("");

                if (!string.IsNullOrEmpty(ticket.TechnicianNotes))
                {
                    sb.AppendLine("*📝 Teknisyen Notları:*");
                    sb.AppendLine(ticket.TechnicianNotes);
                    sb.AppendLine("");
                }

                if (ticket.UsedParts != null && ticket.UsedParts.Any())
                {
                    sb.AppendLine("*🛠 Değişen Parçalar:*");
                    foreach (var usedPart in ticket.UsedParts)
                    {
                        if (usedPart.SparePart == null) usedPart.SparePart = await _unitOfWork.Repository<SparePart>().GetByIdAsync(usedPart.SparePartId);
                        string parcaAdi = usedPart.SparePart != null ? usedPart.SparePart.ProductName : "Yedek Parça";
                        sb.AppendLine($"- {parcaAdi} ({usedPart.Quantity} Adet): {usedPart.TotalPrice:N2} TL");
                    }
                    sb.AppendLine("");
                }

                decimal toplamTutar = ticket.TotalPrice ?? 0;
                sb.AppendLine($"*💰 Toplam Tutar:* {toplamTutar:N2} TL");

                string baseUrl = _configuration["DomainSettings:SorgulamaUrl"];
                if (string.IsNullOrEmpty(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";
                baseUrl = baseUrl.TrimEnd('/');
                string odemeLinki = $"{baseUrl}/Home/Result?fisNo={ticket.FisNo}";

                sb.AppendLine($"*💳 Ödeme/Detay Linki:* {odemeLinki}");
                sb.AppendLine("");
                sb.AppendLine("Bizi tercih ettiğiniz için teşekkür ederiz.");
                sb.AppendLine("- Teknik Servis");

                // Gönderim
                bool basarili = await _whatsAppService.SendMessageAsync(gonderilecekNo, sb.ToString(), ticket.Customer.BranchId);

                return Json(new { success = true, message = $"Mesaj başarıyla gönderildi. ({gonderilecekNo})" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Gönderim Hatası: " + ex.Message });
            }
        }

        // --- GÜNCELLENEN METOD 2: Cihaz Hazır Mesajı Gönder (Eksik Olan) ---
        [HttpPost]
        [Authorize(Roles = "Technician,Admin,Personnel")]
        public async Task<IActionResult> SendReadyMessage(Guid id)
        {
            try
            {
                // 1. Yetki Kontrolü
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null && !currentUser.IsWhatsAppEnabled)
                    return Json(new { success = false, message = "WhatsApp gönderme yetkiniz kapalıdır." });

                // 2. Fişi Getir
                var ticket = await _unitOfWork.Repository<ServiceTicket>()
                    .GetByIdWithIncludesAsync(x => x.Id == id, inc => inc.Customer, inc => inc.DeviceBrand);

                if (ticket == null) return Json(new { success = false, message = "Kayıt bulunamadı." });
                if (ticket.Customer == null || string.IsNullOrEmpty(ticket.Customer.Phone))
                    return Json(new { success = false, message = "Müşterinin telefon numarası kayıtlı değil." });

                // 3. Mesajı Hazırla
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"Sayın *{ticket.Customer.FirstName} {ticket.Customer.LastName}*,");
                sb.AppendLine("");
                sb.AppendLine($"*{ticket.FisNo}* fiş numaralı cihazınızın ({ticket.DeviceBrand?.Name} {ticket.DeviceModel}) işlemleri tamamlanmıştır.");
                sb.AppendLine("Cihazınızı teslim almak için servisimize bekleriz.");
                sb.AppendLine("");
                sb.AppendLine("Bizi tercih ettiğiniz için teşekkür ederiz.");
                sb.AppendLine("- Teknik Servis");

                // 4. Gönder
                await _whatsAppService.SendMessageAsync(ticket.Customer.Phone, sb.ToString(), ticket.Customer.BranchId);

                return Json(new { success = true, message = "Hazır mesajı başarıyla gönderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Gönderim Hatası: " + ex.Message });
            }
        }
    }
}
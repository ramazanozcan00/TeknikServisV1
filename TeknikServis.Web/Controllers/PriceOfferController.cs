using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines; // View Engine için gerekli
using Microsoft.AspNetCore.Mvc.ViewFeatures; // HtmlHelperOptions ve ITempDataProvider için gerekli
using Newtonsoft.Json;
using TeknikServis.Core.Entities;
using TeknikServis.Data.Context;
using TeknikServis.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TeknikServis.Web.Services; // IEmailService için gerekli
using System.IO; // StringWriter için gerekli

namespace TeknikServis.Web.Controllers
{
    // [Authorize] 
    public class PriceOfferController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        // --- YENİ EKLENEN SERVİSLER ---
        private readonly IEmailService _emailService;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public PriceOfferController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IEmailService emailService,
            ICompositeViewEngine viewEngine,
            ITempDataProvider tempDataProvider)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        public async Task<IActionResult> Index()
        {
            var offers = await _context.PriceOffers
                .Include(x => x.Customer)
                .Include(x => x.Branch)
                .OrderByDescending(x => x.OfferDate)
                .ToListAsync();
            return View(offers);
        }

        // --- SAYFA AÇILIŞI (GET) ---
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var userBranchId = user?.BranchId ?? Guid.Empty;

            // Şube boşsa ilk şubeyi al (Güvenlik)
            if (userBranchId == Guid.Empty)
            {
                var firstBranch = await _context.Branches.FirstOrDefaultAsync();
                if (firstBranch != null) userBranchId = firstBranch.Id;
            }

            var model = new PriceOfferViewModel
            {
                DocumentNo = "TKL-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(100, 999),
                BranchId = userBranchId, // BURASI ÖNEMLİ: Kullanıcının şubesi varsayılan seçilir
                Branches = _context.Branches.ToList()
            };

            // Sayfa açılırken o şubenin verilerini hazırla
            await PrepareViewBagsAsync(model, userBranchId);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(PriceOfferViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var offer = new PriceOffer
                    {
                        DocumentNo = model.DocumentNo,
                        OfferDate = model.OfferDate,
                        CustomerId = model.CustomerId,
                        BranchId = model.BranchId,
                        Notes = model.Notes,
                        CreatedDate = DateTime.Now,
                        Items = new List<PriceOfferItem>()
                    };

                    if (!string.IsNullOrEmpty(model.ItemsJson))
                    {
                        var items = JsonConvert.DeserializeObject<List<PriceOfferItemDto>>(model.ItemsJson);
                        decimal grandTotal = 0;

                        foreach (var item in items)
                        {
                            var lineTotal = item.Quantity * item.Price;
                            grandTotal += lineTotal;

                            offer.Items.Add(new PriceOfferItem
                            {
                                SparePartId = item.ProductId,
                                ProductName = item.ProductName,
                                Quantity = item.Quantity,
                                UnitPrice = item.Price,
                                TotalPrice = lineTotal,
                                CreatedDate = DateTime.Now
                            });
                        }
                        offer.TotalAmount = grandTotal;
                    }

                    _context.PriceOffers.Add(offer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Fiyat teklifi kaydedildi.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Hata: " + ex.Message);
                }
            }

            await PrepareViewBagsAsync(model, model.BranchId);
            return View(model);
        }

        // --- 1. MÜŞTERİ DETAYINI GETİR ---
        [HttpGet]
        public async Task<IActionResult> GetCustomerData(Guid customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return Json(null);

            var result = new
            {
                title = !string.IsNullOrEmpty(customer.CompanyName) ? customer.CompanyName : customer.FirstName + " " + customer.LastName,
                phone = customer.Phone ?? "-",
                address = customer.Address ?? "-",
                tax = !string.IsNullOrEmpty(customer.TaxNumber) ? $"{customer.TaxOffice} / {customer.TaxNumber}" : "-",
                email = customer.Email ?? "-"
            };

            return Json(result);
        }

        // --- 2. ŞUBE VERİLERİNİ ÇEKME (Liste doldurma) ---
        [HttpGet]
        public async Task<IActionResult> GetBranchData(Guid branchId)
        {
            // Stoklar
            var parts = await _context.SpareParts
                .Where(x => x.BranchId == branchId)
                .Select(x => new { id = x.Id, productName = x.ProductName, salesPrice = x.SalesPrice, quantity = x.Quantity })
                .ToListAsync();

            // Müşteriler
            var rawCustomers = await _context.Customers
                .Where(x => x.BranchId == branchId)
                .Select(x => new { x.Id, x.CompanyName, x.FirstName, x.LastName, x.Phone })
                .ToListAsync();

            var customers = rawCustomers.Select(x => new {
                id = x.Id,
                text = !string.IsNullOrEmpty(x.CompanyName) ? x.CompanyName : $"{x.FirstName} {x.LastName} - {x.Phone}"
            }).OrderBy(x => x.text).ToList();

            return Json(new { parts, customers });
        }

        private async Task PrepareViewBagsAsync(PriceOfferViewModel model, Guid branchId)
        {
            var rawCustomers = await _context.Customers
                .Where(x => x.BranchId == branchId)
                .Select(c => new { c.Id, c.CompanyName, c.FirstName, c.LastName, c.Phone })
                .ToListAsync();

            model.CustomerList = rawCustomers
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = !string.IsNullOrEmpty(c.CompanyName) ? c.CompanyName : $"{c.FirstName} {c.LastName} - {c.Phone}"
                })
                .OrderBy(x => x.Text)
                .ToList();

            model.Products = await _context.SpareParts.Where(x => x.BranchId == branchId).ToListAsync();
            model.Branches = _context.Branches.ToList();
        }

        // --- YAZDIRMA EKRANI ---
        public async Task<IActionResult> Print(Guid id)
        {
            // 1. Teklif Detaylarını Çek
            var offer = await _context.PriceOffers
                .Include(x => x.Customer)
                .Include(x => x.Branch)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (offer == null) return NotFound();

            // 2. Şube Profil Bilgilerini Çek (BranchInfo Tablosu)
            var branchInfo = await _context.Set<BranchInfo>()
                .FirstOrDefaultAsync(x => x.BranchId == offer.BranchId);

            // View'a gönderiyoruz
            ViewBag.BranchInfo = branchInfo;

            // 3. Giriş Yapan Personelin İsmini Al
            var user = await _userManager.GetUserAsync(User);
            ViewBag.PersonnelName = user != null ? $"{user.FullName}" : "Yetkili Personel";

            return View(offer);
        }

        // --- MAİL GÖNDERME İŞLEMİ (AJAX) ---
        [HttpPost]
        public async Task<IActionResult> SendOfferMail(Guid id)
        {
            try
            {
                var offer = await _context.PriceOffers
                    .Include(x => x.Customer)
                    .Include(x => x.Branch)
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (offer == null) return Json(new { success = false, message = "Teklif bulunamadı." });

                string toEmail = offer.Customer.Email;
                if (string.IsNullOrEmpty(toEmail)) return Json(new { success = false, message = "Müşterinin e-posta adresi kayıtlı değil." });

                // Mail için gerekli verileri tekrar dolduruyoruz (Print action'ı gibi)
                var branchInfo = await _context.Set<BranchInfo>().FirstOrDefaultAsync(x => x.BranchId == offer.BranchId);
                ViewBag.BranchInfo = branchInfo;

                var user = await _userManager.GetUserAsync(User);
                ViewBag.PersonnelName = user != null ? $"{user.FullName}" : "Yetkili Personel";

                ViewBag.IsEmail = true; // Mail modunu aç (Butonları gizlemek için)

                // View'ı HTML String'e Çevir
                string htmlBody = await RenderViewToStringAsync("Print", offer);

                // Mail Gönder
                await _emailService.SendEmailAsync(toEmail, $"Fiyat Teklifi: {offer.DocumentNo}", htmlBody);

                return Json(new { success = true, message = $"Teklif başarıyla {toEmail} adresine gönderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        // --- YARDIMCI METOT: View'ı String'e Çevirir ---
        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            ViewData.Model = model;
            using (var sw = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"{viewName} bulunamadı");
                }

                var viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    sw,
                    new HtmlHelperOptions()
                );
                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
        }


        // --- DÜZENLEME SAYFASI (GET) ---
        public async Task<IActionResult> Edit(Guid id)
        {
            var offer = await _context.PriceOffers
                .Include(x => x.Items) // Kalemleri de çekiyoruz
                .FirstOrDefaultAsync(x => x.Id == id);

            if (offer == null) return NotFound();

            // Veritabanındaki veriyi ViewModel'e dönüştür
            var model = new PriceOfferViewModel
            {
                Id = offer.Id,
                DocumentNo = offer.DocumentNo,
                OfferDate = offer.OfferDate,
                CustomerId = offer.CustomerId,
                BranchId = offer.BranchId ?? Guid.Empty,
                Notes = offer.Notes,

                // Mevcut kalemleri JSON formatına çevirip View'a gönderiyoruz
                ItemsJson = JsonConvert.SerializeObject(offer.Items.Select(x => new PriceOfferItemDto
                {
                    ProductId = x.SparePartId,
                    ProductName = x.ProductName,
                    Quantity = x.Quantity,
                    Price = x.UnitPrice,
                    Total = x.TotalPrice
                }).ToList())
            };

            // Dropdown listelerini doldur
            await PrepareViewBagsAsync(model, model.BranchId);

            return View(model);
        }

        // --- DÜZENLEME İŞLEMİ (POST) ---
        [HttpPost]
        public async Task<IActionResult> Edit(PriceOfferViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Ana Kaydı Bul
                    var offer = await _context.PriceOffers
                        .Include(x => x.Items)
                        .FirstOrDefaultAsync(x => x.Id == model.Id);

                    if (offer == null) return NotFound();

                    // 2. Ana Bilgileri Güncelle
                    offer.OfferDate = model.OfferDate;
                    offer.CustomerId = model.CustomerId;
                    offer.BranchId = model.BranchId;
                    offer.Notes = model.Notes;
                    offer.UpdatedDate = DateTime.Now;

                    // 3. Kalemleri Güncelle (En temiz yol: Eskileri sil, yenileri ekle)
                    // Önce mevcut satırları temizliyoruz
                    _context.RemoveRange(offer.Items);

                    // Yeni listeyi oluşturuyoruz
                    offer.Items = new List<PriceOfferItem>();
                    decimal grandTotal = 0;

                    if (!string.IsNullOrEmpty(model.ItemsJson))
                    {
                        var items = JsonConvert.DeserializeObject<List<PriceOfferItemDto>>(model.ItemsJson);
                        foreach (var item in items)
                        {
                            var lineTotal = item.Quantity * item.Price;
                            grandTotal += lineTotal;

                            offer.Items.Add(new PriceOfferItem
                            {
                                SparePartId = item.ProductId,
                                ProductName = item.ProductName,
                                Quantity = item.Quantity,
                                UnitPrice = item.Price,
                                TotalPrice = lineTotal,
                                CreatedDate = DateTime.Now // veya offer.CreatedDate
                            });
                        }
                    }
                    offer.TotalAmount = grandTotal;

                    // 4. Kaydet
                    _context.PriceOffers.Update(offer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Fiyat teklifi başarıyla güncellendi.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Güncelleme hatası: " + ex.Message);
                }
            }

            // Hata varsa sayfayı tekrar doldur
            await PrepareViewBagsAsync(model, model.BranchId);
            return View(model);
        }

        // --- SİLME İŞLEMİ ---
        public async Task<IActionResult> Delete(Guid id)
        {
            var offer = await _context.PriceOffers.FindAsync(id);
            if (offer != null)
            {
                // İlişkili kalemler Cascade Delete ayarlıysa otomatik silinir.
                // Değilse önce Items silinmeli:
                // var items = _context.PriceOfferItems.Where(x => x.PriceOfferId == id);
                // _context.PriceOfferItems.RemoveRange(items);

                _context.PriceOffers.Remove(offer);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Teklif silindi.";
            }
            return RedirectToAction("Index");
        }

    }
}
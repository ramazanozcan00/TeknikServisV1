using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json;
using TeknikServis.Core.Entities;
using TeknikServis.Data.Context;
using TeknikServis.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TeknikServis.Web.Services;
using System.IO;
using System.Collections.Generic; // List için
using System.Linq; // LINQ için

namespace TeknikServis.Web.Controllers
{
    // [Authorize]
    public class PriceOfferController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
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

        // --- LİSTELEME ---
        public async Task<IActionResult> Index()
        {
            var offers = await _context.PriceOffers
                .Include(x => x.Customer)
                .Include(x => x.Branch)
                .OrderByDescending(x => x.OfferDate)
                .ToListAsync();
            return View(offers);
        }

        // --- OLUŞTURMA SAYFASI (GET) ---
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            // BranchId nullable gelebilir, kontrol ediyoruz
            Guid userBranchId = user?.BranchId ?? Guid.Empty;

            if (userBranchId == Guid.Empty)
            {
                var firstBranch = await _context.Branches.FirstOrDefaultAsync();
                if (firstBranch != null) userBranchId = firstBranch.Id;
            }

            var model = new PriceOfferViewModel
            {
                DocumentNo = "TKL-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(100, 999),
                BranchId = userBranchId,
                OfferDate = DateTime.Now,
                Branches = await _context.Branches.ToListAsync()
            };

            await PrepareViewBagsAsync(model, userBranchId);
            return View(model);
        }

        // --- OLUŞTURMA İŞLEMİ (POST) ---
        [HttpPost]
        public async Task<IActionResult> Create(PriceOfferViewModel model)
        {
            // Validasyon
            if (!ModelState.IsValid)
            {
                await PrepareViewBagsAsync(model, model.BranchId);
                return View(model);
            }

            // Transaction başlat
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // -------------------------------------------------------------
                // A) MÜŞTERİ KONTROLÜ (Kurumsal -> Yeni Müşteri)
                // -------------------------------------------------------------
                Guid finalCustomerId = model.CustomerId;

                // Seçilen ID Customers tablosunda var mı?
                var customerExists = await _context.Customers.AnyAsync(x => x.Id == finalCustomerId);

                if (!customerExists)
                {
                    // Yoksa CompanySettings tablosuna bak (Kurumsal seçilmiş)
                    if (finalCustomerId == Guid.Empty) throw new Exception("Müşteri ID'si boş olamaz.");

                    var companySource = await _context.CompanySettings.FindAsync(finalCustomerId);

                    if (companySource != null)
                    {
                        // Bu vergi no ile daha önce kayıt olmuş mu?
                        var targetCustomer = !string.IsNullOrEmpty(companySource.TaxNumber)
                            ? await _context.Customers.FirstOrDefaultAsync(c => c.TaxNumber == companySource.TaxNumber && !c.IsDeleted)
                            : null;

                        if (targetCustomer != null)
                        {
                            finalCustomerId = targetCustomer.Id;
                        }
                        else
                        {
                            // Yeni Müşteri Oluştur
                            var newCustomer = new Customer
                            {
                                Id = Guid.NewGuid(),
                                BranchId = model.BranchId,
                                CompanyName = companySource.CompanyName,
                                FirstName = "Firma",
                                LastName = "Yetkilisi",
                                Phone = companySource.Phone ?? "-",
                                Address = companySource.Address,
                                TaxOffice = companySource.TaxOffice,
                                TaxNumber = companySource.TaxNumber,
                                CreatedDate = DateTime.Now,
                                IsDeleted = false
                            };
                            _context.Customers.Add(newCustomer);
                            await _context.SaveChangesAsync(); // ID oluşması için SaveChanges şart
                            finalCustomerId = newCustomer.Id;
                        }
                    }
                    else
                    {
                        throw new Exception("Seçilen müşteri veya firma sistemde bulunamadı.");
                    }
                }

                // -------------------------------------------------------------
                // B) TEKLİF BAŞLIĞINI OLUŞTUR
                // -------------------------------------------------------------
                var offer = new PriceOffer
                {
                    Id = Guid.NewGuid(),
                    DocumentNo = model.DocumentNo,
                    OfferDate = model.OfferDate,
                    CustomerId = finalCustomerId, // Kesinleşmiş ID
                    BranchId = model.BranchId,
                    Notes = model.Notes,
                    CreatedDate = DateTime.Now,
                    Status = OfferStatus.Draft,
                    Items = new List<PriceOfferItem>()
                };

                // -------------------------------------------------------------
                // C) ÜRÜNLERİ EKLE (ItemsJson)
                // -------------------------------------------------------------
                if (!string.IsNullOrEmpty(model.ItemsJson))
                {
                    var itemsDto = JsonConvert.DeserializeObject<List<PriceOfferItemDto>>(model.ItemsJson);
                    decimal grandTotal = 0;

                    if (itemsDto != null && itemsDto.Any())
                    {
                        // Ürün ID'lerini toplu çek (Guid? -> Guid dönüşümü yaparak)
                        var productIds = itemsDto
                                        .Where(i => i.ProductId != null && i.ProductId != Guid.Empty)
                                        .Select(i => i.ProductId.GetValueOrDefault())
                                        .ToList();

                        var dbProducts = await _context.SpareParts
                                               .Where(p => productIds.Contains(p.Id))
                                               .ToDictionaryAsync(p => p.Id, p => p.SalesPrice);

                        foreach (var item in itemsDto)
                        {
                            // Güvenli Guid Çevrimi
                            Guid currentProductId = item.ProductId ?? Guid.Empty;

                            // Eğer ID boşsa bu satırı atla
                            if (currentProductId == Guid.Empty) continue;

                            decimal finalPrice = item.Price;

                            // Veritabanı fiyat kontrolü
                            if (dbProducts.TryGetValue(currentProductId, out decimal dbPrice))
                            {
                                finalPrice = dbPrice;
                            }

                            var lineTotal = item.Quantity * finalPrice;
                            grandTotal += lineTotal;

                            var offerItem = new PriceOfferItem
                            {
                                Id = Guid.NewGuid(),
                                PriceOfferId = offer.Id, // İlişkiyi elle de kuruyoruz
                                SparePartId = currentProductId,
                                ProductName = item.ProductName,
                                Quantity = item.Quantity,
                                UnitPrice = finalPrice,
                                TotalPrice = lineTotal,
                                CreatedDate = DateTime.Now
                            };

                            offer.Items.Add(offerItem);
                        }
                    }

                    // GÜVENLİK KONTROLÜ: JSON dolu geldi ama hiç ürün eklenmediyse hata ver.
                    if (itemsDto != null && itemsDto.Any() && !offer.Items.Any())
                    {
                        throw new Exception("Ürünler listesi alındı fakat işlenemedi (Ürün ID hatası).");
                    }

                    offer.TotalAmount = grandTotal;
                }

                // -------------------------------------------------------------
                // D) KAYDET
                // -------------------------------------------------------------
                _context.PriceOffers.Add(offer);
                await _context.SaveChangesAsync();

                // Her şey başarılıysa onayla
                await transaction.CommitAsync();

                TempData["Success"] = "Fiyat teklifi başarıyla oluşturuldu.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Hata varsa işlemleri geri al
                await transaction.RollbackAsync();

                ModelState.AddModelError("", "Kayıt sırasında hata oluştu: " + ex.Message);
                if (ex.InnerException != null) ModelState.AddModelError("", "Detay: " + ex.InnerException.Message);
            }

            await PrepareViewBagsAsync(model, model.BranchId);
            return View(model);
        }

        // --- DİĞER METOTLAR (Edit, Delete, GetBranchData vb. aynı kalabilir) ---
        // ... (Kodu kısaltmak için Edit, Delete vb. metodlarını tekrar yazmadım, onlar önceki düzeltilmiş haliyde kalabilir)

        // --- DÜZENLEME SAYFASI (GET) ---
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var offer = await _context.PriceOffers
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (offer == null) return NotFound();

            Guid currentBranchId = offer.BranchId ?? Guid.Empty;

            var model = new PriceOfferViewModel
            {
                Id = offer.Id,
                DocumentNo = offer.DocumentNo,
                OfferDate = offer.OfferDate,
                CustomerId = offer.CustomerId,
                BranchId = currentBranchId,
                Notes = offer.Notes,
                Branches = await _context.Branches.ToListAsync(),
                ItemsJson = JsonConvert.SerializeObject(offer.Items.Select(x => new PriceOfferItemDto
                {
                    ProductId = x.SparePartId,
                    ProductName = x.ProductName,
                    Quantity = x.Quantity,
                    Price = x.UnitPrice,
                    Total = x.TotalPrice
                }).ToList())
            };

            await PrepareViewBagsAsync(model, currentBranchId);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(PriceOfferViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var offer = await _context.PriceOffers
                        .Include(x => x.Items)
                        .FirstOrDefaultAsync(x => x.Id == model.Id);

                    if (offer == null) return NotFound();

                    offer.OfferDate = model.OfferDate;
                    offer.CustomerId = model.CustomerId;
                    offer.Notes = model.Notes;
                    offer.UpdatedDate = DateTime.Now;

                    if (offer.Items != null && offer.Items.Any())
                    {
                        _context.RemoveRange(offer.Items);
                    }

                    offer.Items = new List<PriceOfferItem>();
                    decimal grandTotal = 0;

                    if (!string.IsNullOrEmpty(model.ItemsJson))
                    {
                        var items = JsonConvert.DeserializeObject<List<PriceOfferItemDto>>(model.ItemsJson);
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                Guid currentProductId = item.ProductId ?? Guid.Empty;
                                if (currentProductId == Guid.Empty) continue;

                                var lineTotal = item.Quantity * item.Price;
                                grandTotal += lineTotal;

                                offer.Items.Add(new PriceOfferItem
                                {
                                    Id = Guid.NewGuid(),
                                    PriceOfferId = offer.Id,
                                    SparePartId = currentProductId,
                                    ProductName = item.ProductName,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.Price,
                                    TotalPrice = lineTotal,
                                    CreatedDate = offer.CreatedDate
                                });
                            }
                        }
                    }
                    offer.TotalAmount = grandTotal;

                    _context.PriceOffers.Update(offer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Teklif başarıyla güncellendi.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Güncelleme Hatası: " + ex.Message);
                }
            }
            await PrepareViewBagsAsync(model, model.BranchId);
            return View(model);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var offer = await _context.PriceOffers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
                if (offer != null)
                {
                    if (offer.Items != null && offer.Items.Any()) _context.RemoveRange(offer.Items);
                    _context.PriceOffers.Remove(offer);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Teklif başarıyla silindi.";
                }
            }
            catch (Exception ex) { TempData["Error"] = "Silme Hatası: " + ex.Message; }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchData(Guid branchId)
        {
            var parts = await _context.SpareParts.Where(x => x.BranchId == branchId && !x.IsDeleted)
                .Select(x => new { id = x.Id, productName = x.ProductName, salesPrice = x.SalesPrice, quantity = x.Quantity }).ToListAsync();

            var individualsRaw = await _context.Customers.Where(x => x.BranchId == branchId && !x.IsDeleted && string.IsNullOrEmpty(x.CompanyName))
                .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).Select(x => new { x.Id, x.FirstName, x.LastName, x.Phone }).ToListAsync();
            var individuals = individualsRaw.Select(x => new { id = x.Id, text = $"{x.FirstName} {x.LastName} - {x.Phone}" }).ToList();

            var companiesRaw = await _context.CompanySettings.Where(x => x.BranchId == branchId && !x.IsDeleted)
                .OrderBy(x => x.CompanyName).Select(x => new { x.Id, x.CompanyName, x.TaxNumber }).ToListAsync();
            var companies = companiesRaw.Select(x => new { id = x.Id, text = $"{x.CompanyName} - {x.TaxNumber}" }).ToList();

            var branchInfoRaw = await _context.Set<BranchInfo>().FirstOrDefaultAsync(x => x.BranchId == branchId);
            var branchBasic = await _context.Branches.FindAsync(branchId);
            string displayTitle = branchBasic?.BranchName ?? "Firma Bilgisi Yok";
            string phoneInfo = "-", addressInfo = "-", taxInfo = "-";
            if (branchInfoRaw != null)
            {
                if (!string.IsNullOrEmpty(branchInfoRaw.CompanyName)) displayTitle = branchInfoRaw.CompanyName;
                else if (!string.IsNullOrEmpty(branchInfoRaw.FirstName)) displayTitle = $"{branchInfoRaw.FirstName} {branchInfoRaw.LastName}";
                phoneInfo = branchInfoRaw.Phone ?? "-"; addressInfo = branchInfoRaw.Address ?? "-";
                taxInfo = !string.IsNullOrEmpty(branchInfoRaw.TaxNumber) ? $"{branchInfoRaw.TaxOffice} / {branchInfoRaw.TaxNumber}" : "-";
            }
            var info = new { title = displayTitle, phone = phoneInfo, address = addressInfo, tax = taxInfo };
            return Json(new { parts, individuals, companies, info });
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerData(Guid customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer != null && !customer.IsDeleted) return Json(new { title = $"{customer.FirstName} {customer.LastName}", phone = customer.Phone ?? "-", address = customer.Address ?? "-", tax = "-", email = customer.Email ?? "-" });
            var company = await _context.CompanySettings.FindAsync(customerId);
            if (company != null && !company.IsDeleted) return Json(new { title = company.CompanyName, phone = company.Phone ?? "-", address = company.Address ?? "-", tax = !string.IsNullOrEmpty(company.TaxNumber) ? $"{company.TaxOffice} / {company.TaxNumber}" : "-", email = "-" });
            return Json(null);
        }

        private async Task PrepareViewBagsAsync(PriceOfferViewModel model, Guid branchId)
        {
            var rawCustomers = await _context.Customers.Where(x => x.BranchId == branchId && !x.IsDeleted).Select(c => new { c.Id, c.CompanyName, c.FirstName, c.LastName, c.Phone }).ToListAsync();
            model.CustomerList = rawCustomers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = !string.IsNullOrEmpty(c.CompanyName) ? $"{c.CompanyName} ({c.FirstName} {c.LastName})" : $"{c.FirstName} {c.LastName} - {c.Phone}" }).OrderBy(x => x.Text).ToList();
            model.Products = await _context.SpareParts.Where(x => x.BranchId == branchId && !x.IsDeleted).ToListAsync();
            model.Branches = await _context.Branches.ToListAsync();
        }

        public async Task<IActionResult> Print(Guid id)
        {
            var offer = await _context.PriceOffers.Include(x => x.Customer).Include(x => x.Branch).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
            if (offer == null) return NotFound();
            var branchId = offer.BranchId ?? Guid.Empty;
            var branchInfo = await _context.Set<BranchInfo>().FirstOrDefaultAsync(x => x.BranchId == branchId);
            ViewBag.BranchInfo = branchInfo;
            var user = await _userManager.GetUserAsync(User);
            ViewBag.PersonnelName = user != null ? $"{user.FullName}" : "Yetkili Personel";
            return View(offer);
        }

        [HttpPost]
        public async Task<IActionResult> SendOfferMail(Guid id)
        {
            try
            {
                var offer = await _context.PriceOffers.Include(x => x.Customer).Include(x => x.Branch).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
                if (offer == null) return Json(new { success = false, message = "Teklif bulunamadı." });
                string toEmail = offer.Customer.Email;
                if (string.IsNullOrEmpty(toEmail)) return Json(new { success = false, message = "Müşterinin e-posta adresi yok." });
                var branchId = offer.BranchId ?? Guid.Empty;
                var branchInfo = await _context.Set<BranchInfo>().FirstOrDefaultAsync(x => x.BranchId == branchId);
                ViewBag.BranchInfo = branchInfo;
                var user = await _userManager.GetUserAsync(User);
                ViewBag.PersonnelName = user != null ? $"{user.FullName}" : "Yetkili Personel";
                ViewBag.IsEmail = true;
                string htmlBody = await RenderViewToStringAsync("Print", offer);
                await _emailService.SendEmailAsync(toEmail, $"Fiyat Teklifi: {offer.DocumentNo}", htmlBody);
                return Json(new { success = true, message = "Mail başarıyla gönderildi." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Hata: " + ex.Message }); }
        }

        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            ViewData.Model = model;
            using (var sw = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
                if (viewResult.View == null) throw new ArgumentNullException($"{viewName} bulunamadı");
                var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw, new HtmlHelperOptions());
                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
        }
    }
}
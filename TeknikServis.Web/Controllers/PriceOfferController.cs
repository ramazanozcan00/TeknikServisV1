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
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
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
        public async Task<IActionResult> Index(int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            int pageSize = 10;

            var query = _context.PriceOffers
                .Include(x => x.Customer)
                .Include(x => x.Branch)
                .AsQueryable();

            if (user != null && user.BranchId != null && user.BranchId != Guid.Empty)
            {
                query = query.Where(x => x.BranchId == user.BranchId);
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var offers = await query
                .OrderByDescending(x => x.OfferDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalItems;

            return View(offers);
        }

        // --- OLUŞTURMA SAYFASI ---
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
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

        // --- OLUŞTURMA İŞLEMİ ---
        [HttpPost]
        public async Task<IActionResult> Create(PriceOfferViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PrepareViewBagsAsync(model, model.BranchId);
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Guid finalCustomerId = model.CustomerId;
                var customerExists = await _context.Customers.AnyAsync(x => x.Id == finalCustomerId);

                if (!customerExists)
                {
                    if (finalCustomerId == Guid.Empty) throw new Exception("Müşteri ID'si boş olamaz.");

                    var companySource = await _context.CompanySettings.FindAsync(finalCustomerId);

                    if (companySource != null)
                    {
                        var targetCustomer = !string.IsNullOrEmpty(companySource.TaxNumber)
                            ? await _context.Customers.FirstOrDefaultAsync(c => c.TaxNumber == companySource.TaxNumber && !c.IsDeleted)
                            : null;

                        if (targetCustomer != null)
                        {
                            finalCustomerId = targetCustomer.Id;
                        }
                        else
                        {
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
                            await _context.SaveChangesAsync();
                            finalCustomerId = newCustomer.Id;
                        }
                    }
                    else
                    {
                        throw new Exception("Seçilen müşteri veya firma sistemde bulunamadı.");
                    }
                }

                var offer = new PriceOffer
                {
                    Id = Guid.NewGuid(),
                    DocumentNo = model.DocumentNo,
                    OfferDate = model.OfferDate,
                    CustomerId = finalCustomerId,
                    BranchId = model.BranchId,
                    Notes = model.Notes,
                    CreatedDate = DateTime.Now,
                    Status = OfferStatus.Draft,
                    Items = new List<PriceOfferItem>()
                };

                if (!string.IsNullOrEmpty(model.ItemsJson))
                {
                    var itemsDto = JsonConvert.DeserializeObject<List<PriceOfferItemDto>>(model.ItemsJson);
                    decimal grandTotal = 0;

                    if (itemsDto != null && itemsDto.Any())
                    {
                        foreach (var item in itemsDto)
                        {
                            Guid? currentProductId = (item.ProductId == null || item.ProductId == Guid.Empty) ? null : item.ProductId;

                            decimal finalPrice = item.Price;
                            var lineTotal = item.Quantity * finalPrice;
                            grandTotal += lineTotal;

                            var offerItem = new PriceOfferItem
                            {
                                Id = Guid.NewGuid(),
                                PriceOfferId = offer.Id,
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
                    offer.TotalAmount = grandTotal;
                }

                _context.PriceOffers.Add(offer);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Fiyat teklifi başarıyla oluşturuldu.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Kayıt sırasında hata oluştu: " + ex.Message);
            }

            await PrepareViewBagsAsync(model, model.BranchId);
            return View(model);
        }

        // --- DÜZENLEME SAYFASI ---
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
                ItemsJson = JsonConvert.SerializeObject(offer.Items.Select(x => new PriceOfferItemDto
                {
                    ProductId = x.SparePartId,
                    ProductName = x.ProductName,
                    Quantity = x.Quantity,
                    Price = x.UnitPrice,
                    Total = x.TotalPrice
                }).ToList())
            };

            await PrepareViewBagsAsync(model, currentBranchId, offer.CustomerId);
            return View(model);
        }

        // --- DÜZENLEME İŞLEMİ (KESİN ÇÖZÜM) ---
        [HttpPost]
        public async Task<IActionResult> Edit(PriceOfferViewModel model)
        {
            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. Önce Ana Teklif Kaydını Çek (Include KULLANMADAN - Tracking karışmaması için)
                    var offer = await _context.PriceOffers.FindAsync(model.Id);

                    if (offer == null) return NotFound();

                    // 2. Ana Bilgileri Güncelle
                    offer.OfferDate = model.OfferDate;
                    offer.CustomerId = model.CustomerId;
                    offer.Notes = model.Notes;
                    offer.UpdatedDate = DateTime.Now;

                    // 3. Mevcut Kalemleri Veritabanından Ayrı Bir Sorguyla Bul ve Sil
                    // Bu yöntem, 'offer.Items' koleksiyonu üzerindeki karmaşayı engeller.
                    var existingItems = await _context.Set<PriceOfferItem>()
                                                      .Where(x => x.PriceOfferId == model.Id)
                                                      .ToListAsync();

                    if (existingItems.Any())
                    {
                        _context.Set<PriceOfferItem>().RemoveRange(existingItems);
                        // Silme işlemini hemen yansıtmak bazen ID çakışmalarını önler
                        await _context.SaveChangesAsync();
                    }

                    // 4. Yeni Kalemleri Oluştur ve Ekle
                    decimal grandTotal = 0;
                    if (!string.IsNullOrEmpty(model.ItemsJson))
                    {
                        var items = JsonConvert.DeserializeObject<List<PriceOfferItemDto>>(model.ItemsJson);
                        if (items != null)
                        {
                            var newOfferItems = new List<PriceOfferItem>();
                            foreach (var item in items)
                            {
                                Guid? currentProductId = (item.ProductId == null || item.ProductId == Guid.Empty) ? null : item.ProductId;
                                var lineTotal = item.Quantity * item.Price;
                                grandTotal += lineTotal;

                                newOfferItems.Add(new PriceOfferItem
                                {
                                    Id = Guid.NewGuid(),
                                    PriceOfferId = offer.Id, // ID'yi manuel set ediyoruz
                                    SparePartId = currentProductId,
                                    ProductName = item.ProductName,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.Price,
                                    TotalPrice = lineTotal,
                                    CreatedDate = DateTime.Now
                                });
                            }

                            // Yeni listeyi doğrudan DbContext'e ekle
                            await _context.Set<PriceOfferItem>().AddRangeAsync(newOfferItems);
                        }
                    }

                    // 5. Toplam Tutarı Güncelle
                    offer.TotalAmount = grandTotal;

                    // Ana teklifi güncelle (Track edildiği için Update çağırmaya gerek yok ama garanti olsun diye State set edilebilir)
                    // _context.Entry(offer).State = EntityState.Modified; 

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Teklif başarıyla güncellendi.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Güncelleme Hatası: " + ex.Message);
                    if (ex.InnerException != null) ModelState.AddModelError("", "Detay: " + ex.InnerException.Message);
                }
            }

            await PrepareViewBagsAsync(model, model.BranchId, model.CustomerId);
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

            var individualsRaw = await _context.Customers.Where(x => x.BranchId == branchId && !x.IsDeleted)
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
            if (customer != null && !customer.IsDeleted) return Json(new { title = $"{customer.FirstName} {customer.LastName}", phone = customer.Phone ?? "-", address = customer.Address ?? "-", tax = !string.IsNullOrEmpty(customer.TaxNumber) ? $"{customer.TaxOffice} / {customer.TaxNumber}" : "-", email = customer.Email ?? "-" });
            var company = await _context.CompanySettings.FindAsync(customerId);
            if (company != null && !company.IsDeleted) return Json(new { title = company.CompanyName, phone = company.Phone ?? "-", address = company.Address ?? "-", tax = !string.IsNullOrEmpty(company.TaxNumber) ? $"{company.TaxOffice} / {company.TaxNumber}" : "-", email = "-" });
            return Json(null);
        }

        private async Task PrepareViewBagsAsync(PriceOfferViewModel model, Guid branchId, Guid? selectedCustomerId = null)
        {
            var query = _context.Customers.Where(x => x.BranchId == branchId && !x.IsDeleted);
            var rawCustomers = await query.Select(c => new { c.Id, c.CompanyName, c.FirstName, c.LastName, c.Phone }).ToListAsync();

            if (selectedCustomerId.HasValue && selectedCustomerId.Value != Guid.Empty)
            {
                if (!rawCustomers.Any(x => x.Id == selectedCustomerId.Value))
                {
                    var selectedCustomer = await _context.Customers
                        .Where(c => c.Id == selectedCustomerId.Value)
                        .Select(c => new { c.Id, c.CompanyName, c.FirstName, c.LastName, c.Phone })
                        .FirstOrDefaultAsync();

                    if (selectedCustomer != null)
                    {
                        rawCustomers.Add(selectedCustomer);
                    }
                }
            }

            model.CustomerList = rawCustomers.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = !string.IsNullOrEmpty(c.CompanyName) ? $"{c.CompanyName} ({c.FirstName} {c.LastName})" : $"{c.FirstName} {c.LastName} - {c.Phone}",
                Selected = selectedCustomerId.HasValue && c.Id == selectedCustomerId.Value
            }).OrderBy(x => x.Text).ToList();

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
        public async Task<IActionResult> SendOfferMail(Guid id, IFormFile pdfBlob)
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
                if (string.IsNullOrEmpty(toEmail)) return Json(new { success = false, message = "Müşterinin e-posta adresi yok." });

                byte[] attachmentData = null;
                string attachmentName = null;

                if (pdfBlob != null && pdfBlob.Length > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        await pdfBlob.CopyToAsync(ms);
                        attachmentData = ms.ToArray();

                        // --- DÜZELTİLEN SATIR BURASI ---
                        attachmentName = $"Teklif_{offer.DocumentNo}.pdf";
                    }
                }

                ViewBag.IsEmail = true;

                // Render için gerekli verileri tekrar doldur
                var branchId = offer.BranchId ?? Guid.Empty;
                var branchInfo = await _context.Set<BranchInfo>().FirstOrDefaultAsync(x => x.BranchId == branchId);
                ViewBag.BranchInfo = branchInfo;

                var user = await _userManager.GetUserAsync(User);
                ViewBag.PersonnelName = user != null ? $"{user.FullName}" : "Yetkili Personel";

                string htmlBody = await RenderViewToStringAsync("Print", offer);

                await _emailService.SendEmailWithAttachmentAsync(
                    toEmail,
                    $"Fiyat Teklifi - {offer.DocumentNo}",
                    "Sayın Müşterimiz,<br>Fiyat teklifiniz ektedir.<br><br>" + htmlBody,
                    attachmentData,
                    attachmentName
                );

                return Json(new { success = true, message = "Teklif PDF olarak mail gönderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Mail gönderme hatası: " + ex.Message });
            }
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
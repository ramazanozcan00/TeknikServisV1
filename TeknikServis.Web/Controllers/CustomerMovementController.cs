using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Data.UnitOfWork;
using TeknikServis.Web.Extensions;
using ClosedXML.Excel;
using System.IO;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class CustomerMovementController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerMovementController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index(int? month, int? year, Guid? customerId)
        {
            // --- YETKİ KONTROLÜ ---
            // Admin değilse VE "CustomerMovements" menü yetkisi (Claim) yoksa engelle
            if (!User.IsInRole("Admin") && !User.HasClaim("MenuAccess", "CustomerMovements"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var branchId = User.GetBranchId();

            // Hareketleri getiriyoruz
            var movements = await _unitOfWork.Repository<CustomerMovement>()
                .FindAsync(x => x.BranchId == branchId, inc => inc.Customer);

            // Filtreleme Mantığı
            if (customerId.HasValue)
                movements = movements.Where(x => x.CustomerId == customerId.Value).ToList();

            if (month.HasValue && month > 0)
                movements = movements.Where(x => x.CreatedDate.Month == month.Value).ToList();

            if (year.HasValue)
                movements = movements.Where(x => x.CreatedDate.Year == year.Value).ToList();

            // Tekrarlayan ve silinen verileri engelliyoruz
            var customers = await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.BranchId == branchId && !x.IsDeleted);

            ViewBag.Customers = customers
                .Select(c => new {
                    Id = c.Id,
                    Name = string.IsNullOrEmpty(c.CompanyName)
                        ? $"{c.FirstName} {c.LastName}"
                        : $"{c.CompanyName} ({c.FirstName} {c.LastName})"
                })
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.SelectedMonth = month;
            ViewBag.SelectedYear = year;
            ViewBag.SelectedCustomer = customerId;

            return View(movements.OrderByDescending(x => x.CreatedDate));
        }

        public async Task<IActionResult> ExportToExcel(int? month, int? year, Guid? customerId)
        {
            // --- YETKİ KONTROLÜ ---
            if (!User.IsInRole("Admin") && !User.HasClaim("MenuAccess", "CustomerMovements"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var branchId = User.GetBranchId();
            var movements = await _unitOfWork.Repository<CustomerMovement>()
                .FindAsync(x => x.BranchId == branchId, inc => inc.Customer);

            if (customerId.HasValue) movements = movements.Where(x => x.CustomerId == customerId.Value).ToList();
            if (month.HasValue && month > 0) movements = movements.Where(x => x.CreatedDate.Month == month.Value).ToList();
            if (year.HasValue) movements = movements.Where(x => x.CreatedDate.Year == year.Value).ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Cari Hareketler");
                var currentRow = 1;

                // Başlıklar
                worksheet.Cell(currentRow, 1).Value = "Tarih";
                worksheet.Cell(currentRow, 2).Value = "Müşteri / Firma";
                worksheet.Cell(currentRow, 3).Value = "Açıklama";
                worksheet.Cell(currentRow, 4).Value = "İşlem Tipi";
                worksheet.Cell(currentRow, 5).Value = "Tutar (TL)";

                worksheet.Range("A1:E1").Style.Font.Bold = true;
                worksheet.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.LightGray;

                foreach (var item in movements.OrderByDescending(x => x.CreatedDate))
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = item.CreatedDate.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cell(currentRow, 2).Value = string.IsNullOrEmpty(item.Customer?.CompanyName)
                        ? $"{item.Customer?.FirstName} {item.Customer?.LastName}"
                        : item.Customer?.CompanyName;
                    worksheet.Cell(currentRow, 3).Value = item.Description;
                    worksheet.Cell(currentRow, 4).Value = item.MovementType;
                    worksheet.Cell(currentRow, 5).Value = item.Amount;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    string fileName = $"Cari_Rapor_{DateTime.Now:yyyyMMddHHmm}.xlsx";

                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        [Authorize(Roles = "Admin,Personnel")]
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var movement = await _unitOfWork.Repository<CustomerMovement>().GetByIdAsync(id);
            if (movement == null) return NotFound();

            // Güvenlik: Sadece kendi şubesindeki hareketi düzenleyebilir
            if (movement.BranchId != User.GetBranchId()) return Forbid();

            return View(movement);
        }

        [Authorize(Roles = "Admin,Personnel")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerMovement model)
        {
            var movement = await _unitOfWork.Repository<CustomerMovement>().GetByIdAsync(model.Id);

            if (movement == null) return NotFound();
            if (movement.BranchId != User.GetBranchId()) return Forbid();

            // DÜZELTME: JS'den gelen güncel metni ve tipi kaydet
            movement.MovementType = model.MovementType;
            movement.Description = model.Description;
            movement.Amount = model.Amount;
            movement.UpdatedDate = DateTime.Now;

            _unitOfWork.Repository<CustomerMovement>().Update(movement);
            await _unitOfWork.CommitAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
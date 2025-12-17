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

using ClosedXML.Excel; // Excel kütüphanesini ekleyin
using System.IO;

namespace TeknikServis.Web.Controllers
{
    public class CustomerMovementController : Controller
    {
        // 1. ADIM: Nesne örneğini tutacak olan değişkeni tanımlayın
        private readonly IUnitOfWork _unitOfWork;

        // 2. ADIM: Constructor (Yapıcı Metod) ile sistemi bu değişkeni doldurmaya zorlayın
        // Program çalıştığında .NET buraya canlı bir UnitOfWork nesnesi teslim eder.
        public CustomerMovementController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [Authorize(Roles = "Admin,Personnel")]
        public async Task<IActionResult> Index(int? month, int? year, Guid? customerId)
        {
            var branchId = User.GetBranchId(); // Aktif şube ID'sini alıyoruz

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

            // --- GÜNCELLEME: Tekrarlayan ve silinen verileri engelliyoruz ---
            var customers = await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.BranchId == branchId && !x.IsDeleted); // Silinmemiş müşterileri filtrele

            ViewBag.Customers = customers
                .Select(c => new {
                    Id = c.Id,
                    Name = string.IsNullOrEmpty(c.CompanyName)
                        ? $"{c.FirstName} {c.LastName}"
                        : $"{c.CompanyName} ({c.FirstName} {c.LastName})"
                })
                .GroupBy(x => x.Id) // ID'ye göre gruplayarak mükerrerliği engelle
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.SelectedMonth = month;
            ViewBag.SelectedYear = year;
            ViewBag.SelectedCustomer = customerId;

            return View(movements.OrderByDescending(x => x.CreatedDate));
        }



        [Authorize(Roles = "Admin,Personnel")]
    public async Task<IActionResult> ExportToExcel(int? month, int? year, Guid? customerId)
    {
        var branchId = User.GetBranchId();
        var movements = await _unitOfWork.Repository<CustomerMovement>()
            .FindAsync(x => x.BranchId == branchId, inc => inc.Customer);

        // Filtreleme Mantığı (Index ile aynı olmalı)
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

            // Stil (Opsiyonel)
            worksheet.Range("A1:E1").Style.Font.Bold = true;
            worksheet.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.LightGray;

            // Veriler
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

            worksheet.Columns().AdjustToContents(); // Sütun genişliklerini ayarla

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                string fileName = $"Cari_Rapor_{DateTime.Now:yyyyMMddHHmm}.xlsx";

                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
    }
}
}
using Microsoft.AspNetCore.Hosting; // Dosya yolu için
using Microsoft.AspNetCore.Http; // IFormFile için
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO; // Dosya işlemleri için
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env; // Fotoğraf kaydetmek için ortam bilgisi

        public TicketApiController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _env = env;
        }

        // --- 1. FORM VERİLERİNİ GETİREN METOD (Şube + Rol Filtreli) ---
        [HttpGet("FormData")]
        public async Task<IActionResult> GetTicketFormData([FromQuery] Guid? branchId)
        {
            try
            {
                // Cihaz Türleri
                var types = (await _unitOfWork.Repository<DeviceType>().GetAllAsync())
                            .Select(x => new { x.Id, x.Name }).OrderBy(x => x.Name).ToList();

                // Markalar
                var brands = (await _unitOfWork.Repository<DeviceBrand>().GetAllAsync())
                             .Select(x => new { x.Id, x.Name }).OrderBy(x => x.Name).ToList();

                // Teknisyenleri Getir (Role ve Şubeye Göre)
                var usersInRole = await _userManager.GetUsersInRoleAsync("Technician");

                // Eğer şube ID geldiyse filtrele
                if (branchId.HasValue && branchId.Value != Guid.Empty)
                {
                    usersInRole = usersInRole.Where(u => u.BranchId == branchId.Value).ToList();
                }

                var technicians = usersInRole
                                  .Select(x => new
                                  {
                                      Id = x.Id,
                                      Name = x.FullName ?? x.UserName
                                  })
                                  .OrderBy(x => x.Name)
                                  .ToList();

                return Ok(new { Types = types, Brands = brands, Technicians = technicians });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Veriler çekilemedi: " + ex.Message);
            }
        }

        // Create Metodunu Güncelleyin
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromForm] ServiceTicketCreateDto model)
        {
            if (model == null) return BadRequest("Veri yok.");

            try
            {
                // ... (Fiş No ve Şube işlemleri aynen kalsın) ...

                // A) Fiş No üretme kısmı AYNI kalsın
                string prefix = "SRV";
                if (model.BranchId != Guid.Empty)
                {
                    var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(model.BranchId);
                    if (branch != null && !string.IsNullOrEmpty(branch.BranchName))
                    {
                        string cleanName = branch.BranchName.Trim().ToUpper();
                        prefix = cleanName.Length >= 3 ? cleanName.Substring(0, 3) : cleanName;
                    }
                }
                string randomPart = new Random().Next(100000, 999999).ToString();
                string newFisNo = $"{prefix}-{randomPart}";


                // B) ÇOKLU FOTOĞRAF YÜKLEME İŞLEMİ (DEĞİŞEN KISIM)
                string photoPaths = ""; // Yolları virgülle birleştirip kaydedeceğiz (Basit çözüm)

                if (model.Photos != null && model.Photos.Count > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    List<string> uploadedPaths = new List<string>();

                    foreach (var file in model.Photos)
                    {
                        if (file.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }
                            uploadedPaths.Add("/uploads/" + uniqueFileName);
                        }
                    }

                    // Veritabanına "url1.jpg;url2.jpg" şeklinde yan yana kaydedelim
                    // (Daha profesyonel çözüm ayrı bir 'TicketPhotos' tablosu kullanmaktır ama bu hızlı çözümdür)
                    photoPaths = string.Join(";", uploadedPaths);
                }

                // C) KAYIT
                var ticket = new ServiceTicket
                {
                    FisNo = newFisNo,
                    // ... (Diğer alanlar aynen kalsın) ...
                    CustomerId = model.CustomerId,
                    BranchId = model.BranchId,
                    DeviceTypeId = model.DeviceTypeId,
                    DeviceBrandId = model.DeviceBrandId,
                    TechnicianId = model.TechnicianId,
                    DeviceModel = model.DeviceModel ?? "",
                    SerialNumber = model.SerialNo ?? "",
                    ProblemDescription = model.Problem ?? "",
                    Accessories = model.Accessories,
                    PhysicalDamage = model.PhysicalDamage,
                    IsWarranty = model.IsWarranty,
                    InvoiceDate = model.InvoiceDate,

                    PhotoPath = photoPaths, // <--- ARTIK BİRLEŞTİRİLMİŞ YOLLARI KAYDEDİYORUZ

                    Status = model.TechnicianId != null ? "İşlemde" : "Bekliyor",
                    TechnicianStatus = model.TechnicianId != null ? "Atandı" : "Atanmadı",
                    CreatedDate = DateTime.Now
                };

                await _unitOfWork.Repository<ServiceTicket>().AddAsync(ticket);
                await _unitOfWork.CommitAsync();

                return Ok(new { Message = "Kayıt Başarılı", FisNo = newFisNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Hata: " + ex.Message);
            }
        }

        // --- 3. ŞİRKET LİSTESİ ---
        [HttpGet("GetCompanies")]
        public async Task<IActionResult> GetCompanies()
        {
            try
            {
                var customers = await _unitOfWork.Repository<Customer>().GetAllAsync();
                var companies = customers
                    .Where(c => !string.IsNullOrEmpty(c.CompanyName))
                    .Select(c => c.CompanyName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                return Ok(companies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Şirket listesi çekilemedi: " + ex.Message);
            }
        }
    }

    // --- DTO (Veri Transfer Objesi) ---
    // Mobil taraftan gelen verileri karşılayan sınıf
    public class ServiceTicketCreateDto
    {
        public Guid BranchId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid DeviceTypeId { get; set; }
        public Guid DeviceBrandId { get; set; }
        public Guid? TechnicianId { get; set; }
        public List<IFormFile> Photos { get; set; }
        public string DeviceModel { get; set; }
        public string SerialNo { get; set; }
        public string Problem { get; set; }
        public string Accessories { get; set; }
        public string PhysicalDamage { get; set; }
        public bool IsWarranty { get; set; }
        public DateTime? InvoiceDate { get; set; }
        // Fotoğraf dosyası için gerekli alan
        public IFormFile Photo { get; set; }
    }
}
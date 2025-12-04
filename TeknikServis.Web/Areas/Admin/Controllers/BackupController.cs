using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Data.Context;
using TeknikServis.Web.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BackupController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TenantService _tenantService;

        public BackupController(
            AppDbContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<AppUser> userManager,
            IUnitOfWork unitOfWork,
            TenantService tenantService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _tenantService = tenantService;
        }

        // --- DASHBOARD (GET) ---
        [HttpGet]
        public async Task<IActionResult> Index(string dbName)
        {
            string currentConnectionString = _context.Database.GetConnectionString();
            var builder = new SqlConnectionStringBuilder(currentConnectionString);
            string mainDbName = builder.InitialCatalog;

            ViewBag.MainDbName = mainDbName;

            // Eğer veritabanı seçilmediyse ana veritabanını seç
            if (string.IsNullOrEmpty(dbName)) dbName = mainDbName;
            ViewBag.SelectedDb = dbName;
            ViewBag.CurrentDbStatus = "Bilinmiyor";

            // 1. Veritabanı Listesi
            var dbSelectList = new List<SelectListItem>();
            bool listFetched = false;

            try
            {
                // Master'dan listeyi çekmeye çalış
                var masterBuilder = new SqlConnectionStringBuilder(currentConnectionString);
                masterBuilder.InitialCatalog = "master";

                using (var masterConn = new SqlConnection(masterBuilder.ConnectionString))
                {
                    await masterConn.OpenAsync();
                    string query = "SELECT name, state_desc FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') ORDER BY name";

                    using (var cmd = new SqlCommand(query, masterConn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string name = reader.GetString(0);
                                string state = reader.GetString(1);

                                dbSelectList.Add(new SelectListItem
                                {
                                    Text = $"{name} ({state})",
                                    Value = name,
                                    Selected = name.Equals(dbName, StringComparison.OrdinalIgnoreCase)
                                });

                                if (name.Equals(dbName, StringComparison.OrdinalIgnoreCase))
                                    ViewBag.CurrentDbStatus = state;
                            }
                            listFetched = true;
                        }
                    }
                }
            }
            catch
            {
                // Master erişimi yoksa veya hata varsa listeyi dolduramayız.
                // Ama en azından ana veritabanını veya seçili olanı listeye ekleyelim.
            }

            if (!listFetched || dbSelectList.Count == 0)
            {
                // Liste boşsa en azından şu anki veritabanını ekle
                dbSelectList.Add(new SelectListItem { Text = dbName, Value = dbName, Selected = true });
            }

            ViewBag.DbList = dbSelectList;

            // 2. Metrikler ve Bağlantı Testi
            // Durum "ONLINE" ise veya "Bilinmiyor" ise bağlanmayı dene.
            if (ViewBag.CurrentDbStatus == "ONLINE" || ViewBag.CurrentDbStatus == "Bilinmiyor")
            {
                try
                {
                    var targetBuilder = new SqlConnectionStringBuilder(currentConnectionString);
                    targetBuilder.InitialCatalog = dbName;
                    targetBuilder.ConnectTimeout = 5; // Hızlı timeout

                    using (var connection = new SqlConnection(targetBuilder.ConnectionString))
                    {
                        await connection.OpenAsync();

                        // Bağlantı başarılıysa durumu ONLINE yap
                        ViewBag.CurrentDbStatus = "ONLINE";

                        // Boyutlar
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = @"SELECT 
                                    SUM(CASE WHEN type_desc = 'ROWS' THEN size * 8.0 / 1024.0 ELSE 0 END) as DataSizeMB,
                                    SUM(CASE WHEN type_desc = 'LOG' THEN size * 8.0 / 1024.0 ELSE 0 END) as LogSizeMB
                                FROM sys.database_files";

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    decimal data = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                                    decimal log = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                                    ViewBag.DataSize = data.ToString("N2");
                                    ViewBag.LogSize = log.ToString("N2");
                                    ViewBag.TotalSize = (data + log).ToString("N2");
                                }
                            }
                        }

                        // Diğer Bilgiler (Hata olsa bile devam et)
                        try
                        {
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = $"SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id = DB_ID('{dbName}')";
                                var count = await cmd.ExecuteScalarAsync();
                                ViewBag.Connections = count?.ToString() ?? "0";
                            }
                        }
                        catch { ViewBag.Connections = "-"; }

                        try
                        {
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = @"SELECT TOP 1 SQLProcessUtilization FROM (SELECT record.value('(./Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int') AS [SQLProcessUtilization], [timestamp] FROM (SELECT [timestamp], CONVERT(xml, record) AS [record] FROM sys.dm_os_ring_buffers WHERE ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR' AND record LIKE '%<SystemHealth>%') AS x) AS y ORDER BY [timestamp] DESC";
                                var cpu = await cmd.ExecuteScalarAsync();
                                ViewBag.CpuUsage = cpu != null ? "%" + cpu.ToString() : "%0";
                            }
                        }
                        catch { ViewBag.CpuUsage = "-"; }
                    }
                }
                catch (Exception ex)
                {
                    // Bağlantı başarısızsa OFFLINE kabul et
                    ViewBag.CurrentDbStatus = "OFFLINE";
                    ViewBag.Error = "Bağlantı Hatası: " + ex.Message;
                    ViewBag.DataSize = "-"; ViewBag.LogSize = "-"; ViewBag.TotalSize = "-";
                }
            }
            else
            {
                ViewBag.DataSize = "-"; ViewBag.LogSize = "-"; ViewBag.TotalSize = "-";
                ViewBag.Connections = "-"; ViewBag.CpuUsage = "-";
            }

            return View();
        }

        // --- DİĞER METOTLAR (CREATE, BACKUP, DELETE) AYNEN KALIYOR ---

        [HttpPost]
        public async Task<IActionResult> CreateDatabase(string dbSuffix)
        {
            if (string.IsNullOrEmpty(dbSuffix)) { TempData["Error"] = "İsim giriniz."; return RedirectToAction("Index"); }
            try
            {
                string safeSuffix = Regex.Replace(dbSuffix, "[^a-zA-Z0-9]", "");
                string newDbName = $"TeknikServis_{safeSuffix}";
                await _tenantService.CreateDatabaseForBranch(newDbName);
                TempData["Success"] = $"'{newDbName}' oluşturuldu.";
                return RedirectToAction("Index", new { dbName = newDbName });
            }
            catch (Exception ex) { TempData["Error"] = "Hata: " + ex.Message; return RedirectToAction("Index"); }
        }

        [HttpPost]
        public async Task<IActionResult> TakeBackup(string dbName)
        {
            if (string.IsNullOrEmpty(dbName)) return RedirectToAction("Index");
            string backupFolder = @"C:\TeknikServisYedekleri";
            try
            {
                if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);
                string fileName = $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                string fullPath = Path.Combine(backupFolder, fileName);
                string sqlCommand = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION, COPY_ONLY";
                var pathParam = new SqlParameter("@path", fullPath);
                await _context.Database.ExecuteSqlRawAsync(sqlCommand, pathParam);
                try
                {
                    var netStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return File(netStream, "application/octet-stream", fileName);
                }
                catch
                {
                    TempData["Success"] = $"Yedek sunucuya alındı: {fullPath}";
                    return RedirectToAction("Index", new { dbName = dbName });
                }
            }
            catch (Exception ex) { TempData["Error"] = "Hata: " + ex.Message; return RedirectToAction("Index", new { dbName = dbName }); }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDatabase(string dbName)
        {
            if (string.IsNullOrEmpty(dbName)) return RedirectToAction("Index");
            string mainDbName = _context.Database.GetDbConnection().Database;
            if (dbName.Equals(mainDbName, StringComparison.OrdinalIgnoreCase)) { TempData["Error"] = "Ana veritabanı silinemez!"; return RedirectToAction("Index", new { dbName = dbName }); }

            try
            {
                // İlişkili verileri sil (Şube, Personel, Müşteri, Servis)
                var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
                var targetBranch = branches.FirstOrDefault(b => b.DatabaseName != null && b.DatabaseName.Equals(dbName, StringComparison.OrdinalIgnoreCase));
                if (targetBranch != null)
                {
                    var users = await _userManager.Users.Where(u => u.BranchId == targetBranch.Id).ToListAsync();
                    foreach (var user in users) await _userManager.DeleteAsync(user);

                    var customers = await _unitOfWork.Repository<Customer>().FindAsync(c => c.BranchId == targetBranch.Id);
                    foreach (var c in customers)
                    {
                        var tickets = await _unitOfWork.Repository<ServiceTicket>().FindAsync(t => t.CustomerId == c.Id);
                        foreach (var t in tickets) _unitOfWork.Repository<ServiceTicket>().Remove(t);
                        _unitOfWork.Repository<Customer>().Remove(c);
                    }
                    _unitOfWork.Repository<Branch>().Remove(targetBranch);
                    await _unitOfWork.CommitAsync();
                }

                // Drop DB
                string connectionString = _context.Database.GetConnectionString();
                var masterBuilder = new SqlConnectionStringBuilder(connectionString);
                masterBuilder.InitialCatalog = "master";
                using (var connection = new SqlConnection(masterBuilder.ConnectionString))
                {
                    await connection.OpenAsync();
                    string sql = $@"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}];";
                    using (var cmd = new SqlCommand(sql, connection)) await cmd.ExecuteNonQueryAsync();
                }
                TempData["Success"] = $"'{dbName}' silindi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex) { TempData["Error"] = "Silme Hatası: " + ex.Message; return RedirectToAction("Index", new { dbName = dbName }); }
        }
    }
}
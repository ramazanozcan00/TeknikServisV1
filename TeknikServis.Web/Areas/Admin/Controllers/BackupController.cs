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
using Microsoft.AspNetCore.Http; // Dosya işlemleri için gerekebilir

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

            if (string.IsNullOrEmpty(dbName)) dbName = mainDbName;
            ViewBag.SelectedDb = dbName;
            ViewBag.CurrentDbStatus = "Bilinmiyor";

            var dbSelectList = new List<SelectListItem>();
            bool listFetched = false;

            try
            {
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
            }

            if (!listFetched || dbSelectList.Count == 0)
            {
                dbSelectList.Add(new SelectListItem { Text = dbName, Value = dbName, Selected = true });
            }

            ViewBag.DbList = dbSelectList;

            if (ViewBag.CurrentDbStatus == "ONLINE" || ViewBag.CurrentDbStatus == "Bilinmiyor")
            {
                try
                {
                    var targetBuilder = new SqlConnectionStringBuilder(currentConnectionString);
                    targetBuilder.InitialCatalog = dbName;
                    targetBuilder.ConnectTimeout = 5;

                    using (var connection = new SqlConnection(targetBuilder.ConnectionString))
                    {
                        await connection.OpenAsync();
                        ViewBag.CurrentDbStatus = "ONLINE";

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

        // --- YENİ VERİTABANI OLUŞTURMA ---
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

        // --- YEDEK ALMA (GÜNCELLENDİ - Error 3 Çözümü İçin) ---
        [HttpPost]
        public async Task<IActionResult> TakeBackup(string dbName)
        {
            if (string.IsNullOrEmpty(dbName)) return RedirectToAction("Index");

            try
            {
                string currentConnectionString = _context.Database.GetConnectionString();
                string backupFilePath = "";
                string fileName = $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";

                // SQL Server'ın varsayılan yedekleme yolunu buluyoruz (Error 3 almamak için)
                string sqlBackupPath = null;
                using (var conn = new SqlConnection(currentConnectionString))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("SELECT CONVERT(nvarchar(500), SERVERPROPERTY('InstanceDefaultBackupPath'))", conn);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    {
                        sqlBackupPath = result.ToString();
                    }
                }

                // Eğer SQL bir yol vermezse C:\TeknikServisYedekleri'ni kullan
                string folderPath = !string.IsNullOrEmpty(sqlBackupPath) ? sqlBackupPath : @"C:\TeknikServisYedekleri";

                if (!folderPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    folderPath += Path.DirectorySeparatorChar;

                backupFilePath = folderPath + fileName;

                // Web sunucusunda klasör oluşturmayı dene (Aynı makinedelerse işe yarar)
                try { if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath); } catch { }

                // Yedeği Al
                string sqlCommand = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION, COPY_ONLY";
                var pathParam = new SqlParameter("@path", backupFilePath);
                await _context.Database.ExecuteSqlRawAsync(sqlCommand, pathParam);

                // Dosyayı İndir
                if (System.IO.File.Exists(backupFilePath))
                {
                    try
                    {
                        var netStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        return File(netStream, "application/octet-stream", fileName);
                    }
                    catch
                    {
                        TempData["Success"] = $"Yedek sunucuya alındı ancak indirilemedi: {backupFilePath}";
                    }
                }
                else
                {
                    // Dosya yoksa SQL Server farklı makinededir
                    TempData["Success"] = $"Yedek SQL Sunucusuna alındı: {backupFilePath}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Yedek Alma Hatası: " + ex.Message;
            }

            return RedirectToAction("Index", new { dbName = dbName });
        }

        // --- SIFIRLAMA (RESET) ---
        [HttpPost]
        public async Task<IActionResult> ResetDatabase(string dbName, string confirmPassword)
        {
            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(confirmPassword))
            {
                TempData["Error"] = "Veritabanı adı veya şifre eksik.";
                return RedirectToAction("Index", new { dbName = dbName });
            }

            string currentConnectionString = _context.Database.GetConnectionString();
            var builder = new SqlConnectionStringBuilder(currentConnectionString);
            string mainDbName = builder.InitialCatalog;

            if (dbName.Equals(mainDbName, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Ana veritabanı (Main_DB) sıfırlanamaz!";
                return RedirectToAction("Index", new { dbName = dbName });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.CheckPasswordAsync(user, confirmPassword))
            {
                TempData["Error"] = "Şifre hatalı! İşlem iptal edildi.";
                return RedirectToAction("Index", new { dbName = dbName });
            }

            try
            {
                SqlConnection.ClearAllPools();
                var masterBuilder = new SqlConnectionStringBuilder(currentConnectionString);
                masterBuilder.InitialCatalog = "master";

                using (var connection = new SqlConnection(masterBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    string killAndDropSql = $@"
                        DECLARE @DatabaseName nvarchar(50) = @dbName;
                        DECLARE @SQL nvarchar(max);
                        SELECT @SQL = COALESCE(@SQL,'') + 'KILL ' + CONVERT(varchar, SPID) + ';'
                        FROM master..SysProcesses
                        WHERE DBId = DB_ID(@DatabaseName) AND SPID <> @@SPID;
                        EXEC(@SQL);

                        IF EXISTS (SELECT name FROM sys.databases WHERE name = @DatabaseName)
                        BEGIN
                            ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE [{dbName}];
                        END";

                    using (var cmd = new SqlCommand(killAndDropSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@dbName", dbName);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await Task.Delay(1000);
                await _tenantService.CreateDatabaseForBranch(dbName);

                TempData["Success"] = $"'{dbName}' başarıyla sıfırlandı.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Sıfırlama Hatası: " + ex.Message;
            }

            return RedirectToAction("Index", new { dbName = dbName });
        }

        // --- SİLME (DELETE) ---
        [HttpPost]
        public async Task<IActionResult> DeleteDatabase(string dbName, string confirmPassword)
        {
            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(confirmPassword)) return RedirectToAction("Index");

            string mainDbName = _context.Database.GetDbConnection().Database;
            if (dbName.Equals(mainDbName, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Ana veritabanı silinemez!";
                return RedirectToAction("Index", new { dbName = dbName });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.CheckPasswordAsync(user, confirmPassword))
            {
                TempData["Error"] = "Şifre hatalı!";
                return RedirectToAction("Index", new { dbName = dbName });
            }

            try
            {
                SqlConnection.ClearAllPools();

                var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
                var targetBranch = branches.FirstOrDefault(b => b.DatabaseName != null && b.DatabaseName.Equals(dbName, StringComparison.OrdinalIgnoreCase));

                if (targetBranch != null)
                {
                    var users = await _userManager.Users.Where(u => u.BranchId == targetBranch.Id).ToListAsync();
                    foreach (var u in users) await _userManager.DeleteAsync(u);

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

                var masterBuilder = new SqlConnectionStringBuilder(_context.Database.GetConnectionString());
                masterBuilder.InitialCatalog = "master";

                using (var connection = new SqlConnection(masterBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    string killAndDropSql = $@"
                        DECLARE @DatabaseName nvarchar(50) = @dbName;
                        DECLARE @SQL nvarchar(max);
                        SELECT @SQL = COALESCE(@SQL,'') + 'KILL ' + CONVERT(varchar, SPID) + ';'
                        FROM master..SysProcesses
                        WHERE DBId = DB_ID(@DatabaseName) AND SPID <> @@SPID;
                        EXEC(@SQL);

                        IF EXISTS (SELECT name FROM sys.databases WHERE name = @DatabaseName)
                        BEGIN
                            ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE [{dbName}];
                        END";

                    using (var cmd = new SqlCommand(killAndDropSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@dbName", dbName);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["Success"] = $"'{dbName}' tamamen silindi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Silme Hatası: " + ex.Message;
                return RedirectToAction("Index", new { dbName = dbName });
            }
        }
    }
}
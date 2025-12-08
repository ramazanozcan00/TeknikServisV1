using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using TeknikServis.Data.Context;

namespace TeknikServis.Web.Services
{
    public interface IBackupService
    {
        Task RunDailyBackupAsync();
    }

    public class BackupService : IBackupService
    {
        private readonly AppDbContext _context;

        public BackupService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RunDailyBackupAsync()
        {
            // 1. Ana veritabanı ismini bul
            var connStr = _context.Database.GetConnectionString();
            var builder = new SqlConnectionStringBuilder(connStr);
            string mainDbName = builder.InitialCatalog;

            // Ana veritabanını yedekle
            await BackupSingleDatabase(mainDbName);

            // Eğer şube sisteminiz varsa ve her şubenin ayrı veritabanı varsa, 
            // buraya diğer veritabanlarını döngüye alacak bir sorgu ekleyebilirsiniz.
        }

        private async Task BackupSingleDatabase(string dbName)
        {
            try
            {
                string fileName = $"{dbName}_AutoBackup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                string sqlBackupPath = null;

                // BackupController'daki yol bulma mantığı
                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("SELECT CONVERT(nvarchar(500), SERVERPROPERTY('InstanceDefaultBackupPath'))", conn);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    {
                        sqlBackupPath = result.ToString();
                    }
                }

                string folderPath = !string.IsNullOrEmpty(sqlBackupPath) ? sqlBackupPath : @"C:\TeknikServisYedekleri";

                if (!folderPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    folderPath += Path.DirectorySeparatorChar;

                string backupFilePath = folderPath + fileName;

                // Klasör yoksa oluşturmaya çalış (Yetki varsa)
                try { if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath); } catch { }

                // SQL Komutunu Çalıştır
                string sqlCommand = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION, COPY_ONLY";
                var pathParam = new SqlParameter("@path", backupFilePath);

                // Timeout süresini artıralım çünkü backup uzun sürebilir
                await _context.Database.ExecuteSqlRawAsync(sqlCommand, pathParam);
            }
            catch (Exception ex)
            {
                // Hata durumunda loglama yapılabilir (Örn: ILogger kullanarak)
                Console.WriteLine($"Yedekleme Hatası ({dbName}): " + ex.Message);
            }
        }
    }
}
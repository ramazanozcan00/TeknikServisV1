using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic; // List<> için eklendi
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
            // Yedeklenecek veritabanı isimlerini tutacak liste
            var databaseNames = new List<string>();

            // 1. Mevcut bağlantı dizesini al
            var connStr = _context.Database.GetConnectionString();

            // 2. Master veritabanına bağlanıp tüm veritabanlarını listele
            // BackupController'daki mantığın aynısını buraya uyguluyoruz.
            var masterBuilder = new SqlConnectionStringBuilder(connStr);
            masterBuilder.InitialCatalog = "master"; // Master'a bağlanmamız lazım listeyi görmek için

            try
            {
                using (var conn = new SqlConnection(masterBuilder.ConnectionString))
                {
                    await conn.OpenAsync();

                    // Sistem veritabanları hariç, ONLINE durumdaki tüm veritabanlarını getir
                    string query = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') AND state_desc = 'ONLINE'";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                databaseNames.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Veritabanı listesi alınırken hata oluştu: " + ex.Message);
                // Hata durumunda en azından ana veritabanını yedeklemeyi dene
                var builder = new SqlConnectionStringBuilder(connStr);
                databaseNames.Add(builder.InitialCatalog);
            }

            // 3. Bulunan tüm veritabanlarını sırayla yedekle
            foreach (var dbName in databaseNames)
            {
                await BackupSingleDatabase(dbName);
            }
        }

        private async Task BackupSingleDatabase(string dbName)
        {
            try
            {
                string fileName = $"{dbName}_AutoBackup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                string sqlBackupPath = null;

                // SQL Server'ın varsayılan yedekleme yolunu bul
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

                // Klasör yoksa oluşturmaya çalış
                try { if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath); } catch { }

                // SQL Komutunu Çalıştır
                // Not: Bağlantı dizesi ana veritabanı olsa bile, yetki varsa başka veritabanının yedeği alınabilir.
                string sqlCommand = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION, COPY_ONLY";
                var pathParam = new SqlParameter("@path", backupFilePath);

                // Timeout süresini artıralım çünkü backup uzun sürebilir
                // ExecuteSqlRawAsync mevcut context (ana db) üzerinden çalışır ama komut hedef db'yi belirtir.
                await _context.Database.ExecuteSqlRawAsync(sqlCommand, pathParam);

                Console.WriteLine($"{dbName} yedeği başarıyla alındı: {backupFilePath}");
            }
            catch (Exception ex)
            {
                // Hata durumunda loglama
                Console.WriteLine($"Yedekleme Hatası ({dbName}): " + ex.Message);
            }
        }
    }
}
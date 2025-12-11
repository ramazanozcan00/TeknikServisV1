using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeknikServis.Data.Context;

namespace TeknikServis.Web.Services
{
    public class TenantService
    {
        private readonly IConfiguration _configuration;

        public TenantService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // --- MEVCUT VERİTABANLARINI LİSTELE ---
        public List<string> GetDatabaseList()
        {
            var list = new List<string>();
            try
            {
                string masterConnString = _configuration.GetConnectionString("Default");
                var builder = new SqlConnectionStringBuilder(masterConnString);
                // Listeyi master üzerinden çekiyoruz
                builder.InitialCatalog = "master";

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                    // Sistem DB'leri hariç, sadece erişilebilir (State=0) olanları getir
                    string sql = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') AND state = 0 ORDER BY name";

                    using (var cmd = new SqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch
            {
                // Bağlantı hatası olursa boş liste dön, uygulama çökmesin.
            }
            return list;
        }

        // --- YENİ VERİTABANI OLUŞTUR ---
        public async Task CreateDatabaseForBranch(string newDbName)
        {
            string masterConnString = _configuration.GetConnectionString("Default");
            var builder = new SqlConnectionStringBuilder(masterConnString);
            builder.InitialCatalog = "master"; // Master veritabanına bağlan

            // 1. Veritabanını Oluştur
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();

                // DB var mı kontrol et
                var checkCmd = new SqlCommand($"SELECT database_id FROM sys.databases WHERE Name = '{newDbName}'", connection);
                var result = await checkCmd.ExecuteScalarAsync();

                if (result == null)
                {
                    // Yoksa oluştur
                    var createCmd = new SqlCommand($"CREATE DATABASE [{newDbName}]", connection);
                    await createCmd.ExecuteNonQueryAsync();
                }
            }

            // 2. Tabloları Oluştur (Migration Uygula)
            try
            {
                // Yeni oluşturulan DB'ye bağlanacak context'i hazırla
                builder.InitialCatalog = newDbName;
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseSqlServer(builder.ConnectionString);

                using (var newContext = new AppDbContext(optionsBuilder.Options))
                {
                    // Veritabanına tabloları bas
                    await newContext.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                // Veritabanı oluştu ama tablolar basılamadıysa hatayı fırlat
                throw new Exception($"Veritabanı '{newDbName}' oluşturuldu ancak tablolar kurulamadı. Hata: {ex.Message}");
            }
        }

        // --- TÜM VERİTABANLARINI GÜNCELLE (MIGRATION) ---
        // Bu metot uygulama başladığında çalışarak tüm DB'leri son versiyona çeker.
        public async Task UpdateAllDatabasesAsync()
        {
            var databases = GetDatabaseList();
            string masterConnString = _configuration.GetConnectionString("Default");

            foreach (var dbName in databases)
            {
                try
                {
                    var builder = new SqlConnectionStringBuilder(masterConnString);
                    builder.InitialCatalog = dbName;

                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseSqlServer(builder.ConnectionString);

                    using (var context = new AppDbContext(optionsBuilder.Options))
                    {
                        // Bekleyen migration varsa uygula (yeni tablo, kolon vs.)
                        await context.Database.MigrateAsync();
                    }
                }
                catch (Exception)
                {
                    // Olası hatalarda (örn: yetki yok, db bozuk) döngü kırılmasın, diğerlerine geçsin.
                    // Loglama yapılabilir.
                    continue;
                }
            }
        }

        // --- BAĞLANTI CÜMLESİ OLUŞTUR ---
        public string GetConnectionString(string dbName)
        {
            string masterConnString = _configuration.GetConnectionString("Default");
            var builder = new SqlConnectionStringBuilder(masterConnString);
            builder.InitialCatalog = dbName;
            return builder.ConnectionString;
        }
    }
}
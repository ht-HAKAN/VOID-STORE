using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace VOID_STORE
{
    public static class DatabaseManager
    {
        // Local MSSQL bağlantı dizesi
        private static readonly string ConnectionString = "Server=.;Database=VOID_STORE_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SqlConnection GetConnection()
        {
            // Veritabanı bağlantı (SqlConnection) nesnesini oluştur.
            // Bu nesne, uygulamanın SQL Server ile iletişim kurmasını sağlar.
            return new SqlConnection(ConnectionString);
        }

        // Sorguyu çalıştır ve veritabanına veri ekle, güncelle veya sil. 
        public static void ExecuteNonQuery(string query, params SqlParameter[] parameters)
        {
            // using bloğu bağlantı nesnesinin işlem bitiminde bellekte yer tutmaması için otomatik olarak kapat.
            using (SqlConnection conn = GetConnection())
            {
                // Veritabanına gönderilecek olan SQL komutunu oluştur.
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // null değilse, işlem sırasında SQL Injection'u engellemek için parametreleri komuta ekle
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    
                    // Veritabanı bağlantısını aktif hale getir.
                    conn.Open();
                    // Hazırlanan SQL komutunu veritabanı üzerinde çalıştır. 
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Sorguyu çalıştır ve veritabanından dönen satır ile sütunları bir veri tablosu formatında döndür.
        public static DataTable ExecuteQuery(string query, params SqlParameter[] parameters)
        {
            // Veritabanı bağlantısını aç ve işlem bitince kapat.
            using (SqlConnection conn = GetConnection())
            {
                // Gelen sorgu ve bağlantı nesnesi ile yeni bir SQL komutu oluştur.
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    
                    // Veritabanından dönen satırları ve sütunları hafızadaki bir C# tablosuna eşle ve doldur
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable(); // Verilerin tutulacağı sanal tablo
                        adapter.Fill(dt); // Tabloyu veritabanından gelen verilerle doldur
                        return dt; // Tabloyu döndür
                    }
                }
            }
        }
        
        // Sorguyu çalıştır ve sorgu sonucundaki ilk satırın ilk sütununu tek bir nesne (obje) olarak döndür.
        public static object ExecuteScalar(string query, params SqlParameter[] parameters)
        {
            // Bağlantı nesnesini oluştur.
            using (SqlConnection conn = GetConnection())
            {
                // SQL komutunu oluştur.
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    // Veritabanı bağlantısını aktif hale getir.
                    conn.Open();
                    // Sorgu sonucundaki ilk satırın ilk sütununu oku ve obje olarak döndür.
                    return cmd.ExecuteScalar();
                }
            }
        }
    }
}

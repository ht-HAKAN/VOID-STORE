using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace VOID_STORE.Models
{
    public static class DatabaseManager
    {
        // local mssql baglanti dizesi
        private static readonly string ConnectionString = "Server=.;Database=VOID_STORE_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SqlConnection GetConnection()
        {
            // veritabani baglanti (sqlconnection) nesnesini olustur
            // bu nesne uygulamanin sql server ile iletisim kurmasini saglar
            return new SqlConnection(ConnectionString);
        }

        // sorguyu calistir ve veritabanina veri ekle guncelle veya sil 
        public static void ExecuteNonQuery(string query, params SqlParameter[] parameters)
        {
            // using blogu baglanti nesnesinin islem bitiminde bellekte yer tutmamasi icin otomatik olarak kapat
            using (SqlConnection conn = GetConnection())
            {
                // veritabanina gonderilecek olan sql komutunu olustur
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // null degilse islem sirasinda sql injectionu engellemek icin parametreleri komuta ekle
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    
                    // veritabani baglantisini aktif hale getir
                    conn.Open();
                    // hazirlanan sql komutunu veritabani uzerinde calistir
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // sorguyu calistir ve veritabanindan donen satir ile sutunlari bir veri tablosu formatinda dondur
        public static DataTable ExecuteQuery(string query, params SqlParameter[] parameters)
        {
            // veritabani baglantisini ac ve islem bitince kapat
            using (SqlConnection conn = GetConnection())
            {
                // gelen sorgu ve baglanti nesnesi ile yeni bir sql komutu olustur
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    
                    // veritabanindan donen satirlari ve sutunlari hafizadaki bir c# tablosuna esle ve doldur
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable(); // verilerin tutulacagi sanal tablo
                        adapter.Fill(dt); // tabloyu veritabanindan gelen verilerle doldur
                        return dt; // tabloyu dondur
                    }
                }
            }
        }
        
        // sorguyu calistir ve sorgu sonucundaki ilk satirin ilk sutununu tek bir nesne obje olarak dondur
        public static object ExecuteScalar(string query, params SqlParameter[] parameters)
        {
            // baglanti nesnesini olustur
            using (SqlConnection conn = GetConnection())
            {
                // sql komutunu olustur
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    // veritabani baglantisini aktif hale getir
                    conn.Open();
                    // sorgu sonucundaki ilk satirin ilk sutununu oku ve obje olarak dondur
                    return cmd.ExecuteScalar();
                }
            }
        }
    }
}

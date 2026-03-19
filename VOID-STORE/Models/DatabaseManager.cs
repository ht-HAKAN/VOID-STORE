using System;
using System.Data;
using SqlCommand = MySql.Data.MySqlClient.MySqlCommand;
using SqlConnection = MySql.Data.MySqlClient.MySqlConnection;
using SqlDataAdapter = MySql.Data.MySqlClient.MySqlDataAdapter;
using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;

namespace VOID_STORE.Models
{
    public static class DatabaseManager
    {
        // yerel mysql baglanti dizesi
        private static readonly string ConnectionString = "Server=127.0.0.1;Port=3306;Database=void_store_db;Uid=root;Pwd=;SslMode=Disabled;CharSet=utf8mb4;";

        public static SqlConnection GetConnection()
        {
            // veritabani baglantisini olustur
            return new SqlConnection(ConnectionString);
        }

        // veri degistiren sorguyu calistir
        public static void ExecuteNonQuery(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // satir donen sorguyu calistir
        public static DataTable ExecuteQuery(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }
        
        // tek deger donen sorguyu calistir
        public static object ExecuteScalar(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    conn.Open();
                    return cmd.ExecuteScalar();
                }
            }
        }
    }
}

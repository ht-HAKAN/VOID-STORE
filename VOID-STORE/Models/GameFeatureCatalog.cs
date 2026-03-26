using System;
using System.Collections.Generic;
using System.Linq;

namespace VOID_STORE.Models
{
    public static class GameFeatureCatalog
    {
        // seçilebilir oyun özellikleri
        public static IReadOnlyList<string> All { get; } = new List<string>
        {
            "Tek Oyunculu",
            "Çok Oyunculu",
            "Eşli Oyun",
            "Çevrim İçi Eşli Oyun",
            "PvP",
            "Denetleyici Desteği",
            "Çapraz Platform",
            "Bulut Kayıtları"
        };

        public static List<string> NormalizeMany(IEnumerable<string>? values)
        {
            // seçimi geçerli listeye uyarla
            if (values == null)
            {
                return new List<string>();
            }

            return values
                .Select(value => value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => All.FirstOrDefault(item => item.Equals(value, StringComparison.OrdinalIgnoreCase)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();
        }
    }
}

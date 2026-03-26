using System;
using System.Collections.Generic;
using System.Linq;

namespace VOID_STORE.Models
{
    public static class GameCategoryCatalog
    {
        // kullanılan kategori listesi
        public static IReadOnlyList<string> All { get; } = new List<string>
        {
            "Aksiyon",
            "Macera",
            "MMORPG",
            "FPS",
            "RPG",
            "Strateji",
            "Korku",
            "Yarış",
            "Spor",
            "Simülasyon"
        };

        // varsayılan kategori
        public static string Default => All[0];

        public static string Normalize(string? value)
        {
            // gelen değeri geçerli kategoriye uyarla
            if (string.IsNullOrWhiteSpace(value))
            {
                return Default;
            }

            string normalizedValue = value.Trim();
            string? matchedValue = All.FirstOrDefault(
                category => category.Equals(normalizedValue, StringComparison.OrdinalIgnoreCase));

            return matchedValue ?? Default;
        }
    }
}

using System.Collections.Generic;

namespace VOID_STORE.Models
{
    public class GameCreateRequest
    {
        // yeni oyunun temel alanlari
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PriceText { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Developer { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string ReleaseDateText { get; set; } = string.Empty;
        public string TrailerVideoSourcePath { get; set; } = string.Empty;
        public string MinimumRequirements { get; set; } = string.Empty;
        public string RecommendedRequirements { get; set; } = string.Empty;
        public string SupportedLanguages { get; set; } = string.Empty;

        // secilen kapak dosyasi
        public string CoverImageSourcePath { get; set; } = string.Empty;

        // secilen platform ve galeri listesi
        public List<string> Platforms { get; set; } = new();
        public List<string> Features { get; set; } = new();
        public List<string> GalleryImageSourcePaths { get; set; } = new();

        // Yeni Alanlar (Ucretsiz & Indirim)
        public bool IsFree { get; set; }
        public int DiscountRate { get; set; }
        public System.DateTime? DiscountStartDate { get; set; }
        public System.DateTime? DiscountEndDate { get; set; }
    }
}

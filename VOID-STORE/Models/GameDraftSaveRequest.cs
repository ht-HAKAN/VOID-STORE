using System.Collections.Generic;

namespace VOID_STORE.Models
{
    public class GameDraftSaveRequest
    {
    // oyunun kimlik bilgisi
        public int GameId { get; set; }

    // guncellenen temel alanlar
        public string Title { get; set; } = string.Empty;

        public string PriceText { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Developer { get; set; } = string.Empty;

        public string Publisher { get; set; } = string.Empty;

        public string ReleaseDateText { get; set; } = string.Empty;

        public string TrailerUrl { get; set; } = string.Empty;

        public string MinimumRequirements { get; set; } = string.Empty;

        public string RecommendedRequirements { get; set; } = string.Empty;

        public string SupportedLanguages { get; set; } = string.Empty;

    // guncellenen medya dosyalari
        public string CoverImageSourcePath { get; set; } = string.Empty;

    // guncellenen platformlar ve galeri dosyalari
        public List<string> Platforms { get; set; } = new();

        public List<string> GalleryImageSourcePaths { get; set; } = new();
    }
}

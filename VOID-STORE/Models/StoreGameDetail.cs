using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace VOID_STORE.Models
{
    public class StoreGameDetail
    {
        public int GameId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PriceText { get; set; } = string.Empty;

        public string CoverImagePath { get; set; } = string.Empty;

        public BitmapImage? CoverPreview { get; set; }

        public string Developer { get; set; } = string.Empty;

        public string Publisher { get; set; } = string.Empty;

        public string ReleaseDateText { get; set; } = string.Empty;

        public string TrailerVideoPath { get; set; } = string.Empty;

        public string MinimumRequirements { get; set; } = string.Empty;

        public string RecommendedRequirements { get; set; } = string.Empty;

        public string SupportedLanguages { get; set; } = string.Empty;

        public List<string> Platforms { get; set; } = new();

        public List<string> Features { get; set; } = new();

        public List<StoreMediaItem> MediaItems { get; set; } = new();
    }
}

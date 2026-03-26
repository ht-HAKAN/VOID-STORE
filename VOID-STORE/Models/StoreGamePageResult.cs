using System.Collections.Generic;

namespace VOID_STORE.Models
{
    public class StoreGamePageResult
    {
        public IReadOnlyList<StoreGameCardItem> Items { get; set; } = new List<StoreGameCardItem>();

        public int TotalCount { get; set; }

        public int CurrentPage { get; set; }

        public int TotalPages { get; set; }
    }
}

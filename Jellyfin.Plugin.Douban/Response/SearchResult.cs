using System.Collections.Generic;

namespace Jellyfin.Plugin.Douban.Response
{
    internal class SearchResult
    {
        public List<SearchSubject> Items { get; set; }

        public int Total { get; set; }
    }

    internal class SearchSubject
    {
        public SearchTarget Target { get; set; }
        public string Target_Type { get; set; }
    }

    internal class SearchTarget
    {
        public string Id { get; set; }
    }
}
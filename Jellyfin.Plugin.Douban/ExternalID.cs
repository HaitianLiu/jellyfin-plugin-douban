using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.Douban
{
    public class DoubanExternalId : IExternalId
    {
        public string Name => "Douban";

        public string Key => "DoubanID";

        public string UrlFormatString => "https://movie.douban.com/subject/{0}/";

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is Series;
        }
    }
}
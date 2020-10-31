using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Douban
{
    public class TVProvider : BaseProvider, IHasOrder,
        IRemoteMetadataProvider<Series, SeriesInfo>,
        IRemoteMetadataProvider<Season, SeasonInfo>,
        IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        public String Name => "Douban TV Provider";
        public int Order => 3;

        public TVProvider(IHttpClient httpClient,
                          IJsonSerializer jsonSerializer,
                          ILogger<TVProvider> logger) : base(httpClient, jsonSerializer, logger)
        {
            // empty
        }

        #region series
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Douban:GetMetadata name: {info.Name}");

            var sid = info.GetProviderId(ProviderID);
            _logger.LogInformation($"sid: {sid}");
            if (string.IsNullOrWhiteSpace(sid))
            {
                var sidList = await SearchSidByName(info.Name, "tv", cancellationToken).ConfigureAwait(false);
                foreach (var s in sidList)
                {
                    _logger.LogDebug($"sidList: {s}");
                }
                sid = sidList.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(sid))
            {
                // Not found, just return
                return new MetadataResult<Series>();
            }

            var result = await GetMetaFromDouban<Series>(sid, "tv",
                cancellationToken).ConfigureAwait(false);
            if (result.HasMetadata)
            {
                info.SetProviderId(ProviderID, sid);
                result.QueriedById = true;
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
            SeriesInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Douban: search name {0}", info.Name);

            var results = new List<RemoteSearchResult>();

            IEnumerable<string> sidList;

            string doubanId = info.GetProviderId(ProviderID);
            _logger.LogInformation("douban id is {0}", doubanId);
            if (!string.IsNullOrEmpty(doubanId))
            {
                sidList = new List<string>
                {
                    doubanId
                };
            }
            else
            {
                sidList = await SearchSidByName(info.Name, "tv", cancellationToken).ConfigureAwait(false);
            }

            foreach (String sid in sidList)
            {
                var itemData = await GetDoubanSubject(sid, "tv", cancellationToken).
                    ConfigureAwait(false);
                if (itemData.Subtype != "tv")
                {
                    continue;
                }

                var searchResult = new RemoteSearchResult()
                {
                    Name = itemData.Title,
                    ImageUrl = itemData.Images.Large,
                    Overview = itemData.Intro,
                    ProductionYear = int.Parse(itemData.Year),
                };
                searchResult.SetProviderId(ProviderID, sid);
                results.Add(searchResult);
            }

            return results;
        }
        #endregion series

        #region season
        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Douban:GetMetadata for {info.Name}");
            var result = new MetadataResult<Season>();

            info.SeriesProviderIds.TryGetValue(ProviderID, out string sid);
            if (string.IsNullOrEmpty(sid))
            {
                _logger.LogInformation("No douban sid found, just skip");
                return result;
            }

            if (info.IndexNumber.HasValue && info.IndexNumber.Value > 0)
            {
                // We can not give more information from Douban right now.
                return result;
            }

            var itemData = await GetDoubanSubject(sid, "tv", cancellationToken).
                ConfigureAwait(false);
            if (itemData.Current_Season.HasValue)
            {
                result.Item = new Season
                {
                    IndexNumber = itemData.Current_Season.Value,
                    ProductionYear = int.Parse(itemData.Year)
                };
                result.HasMetadata = true;
            }
            return result;

        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
            SeasonInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Douban:Search for season {0}", info.Name);
            // It's needless for season to do search
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(
                new List<RemoteSearchResult>());
        }
        #endregion season

        #region episode
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info,
                                              CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Douban:GetMetadata for episode {info.Name}");
            var result = new MetadataResult<Episode>();

            if (info.IsMissingEpisode)
            {
                _logger.LogInformation("Do not support MissingEpisode");
                return result;
            }

            info.SeriesProviderIds.TryGetValue(ProviderID, out string sid);
            if (string.IsNullOrEmpty(sid))
            {
                _logger.LogInformation("No douban sid found, just skip");
                return result;
            }

            if (!info.IndexNumber.HasValue)
            {
                _logger.LogInformation("No episode num found, please check " +
                    "the format of file name");
                return result;
            }
            // Start to get information from douban
            result.Item = new Episode
            {
                Name = info.Name,
                IndexNumber = info.IndexNumber,
                ParentIndexNumber = info.ParentIndexNumber
            };

            var url = String.Format("https://movie.douban.com/subject/{0}" +
                "/episode/{1}/", sid, info.IndexNumber);
            String content = await _doubanAccessor.GetResponseWithDelay(url, new Dictionary<string, string>(), cancellationToken);
            String pattern_name = "data-name=\\\"(.*?)\\\"";
            Match match = Regex.Match(content, pattern_name);
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                _logger.LogDebug("The name is {0}", name);
                result.Item.Name = name;
            }

            String pattern_desc = "data-desc=\\\"(.*?)\\\"";
            match = Regex.Match(content, pattern_desc);
            if (match.Success)
            {
                var desc = match.Groups[1].Value;
                _logger.LogDebug("The desc is {0}", desc);
                result.Item.Overview = desc;
            }
            result.HasMetadata = true;

            return result;
        }


        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
            EpisodeInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Douban:Search for episode {0}", info.Name);
            // It's needless for season to do search
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(
                new List<RemoteSearchResult>());
        }
        #endregion episode
    }
}

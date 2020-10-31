using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Douban
{
    public class MovieProvider : BaseProvider, IHasOrder,
        IRemoteMetadataProvider<Movie, MovieInfo>
    {
        public String Name => "Douban Movie Provider";
        public int Order => 3;

        public MovieProvider(IHttpClient httpClient,
                              IJsonSerializer jsonSerializer,
                              ILogger<MovieProvider> logger) : base(httpClient, jsonSerializer, logger)
        {
            // Empty
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Douban:GetMetadata movie name: {info.Name}");

            var sid = info.GetProviderId(ProviderID);
            if (string.IsNullOrWhiteSpace(sid))
            {
                var sidList = await SearchSidByName(info.Name, "movie",
                    cancellationToken).ConfigureAwait(false);
                sid = sidList.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(sid))
            {
                // Not found, just return
                return new MetadataResult<Movie>();
            }

            var result = await GetMetaFromDouban<Movie>(sid, "movie",
                cancellationToken).ConfigureAwait(false);
            if (result.HasMetadata)
            {
                info.SetProviderId(ProviderID, sid);
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
            MovieInfo info, CancellationToken cancellationToken)
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
                sidList = await SearchSidByName(info.Name, "movie", cancellationToken).
                    ConfigureAwait(false);
            }

            foreach (String sid in sidList)
            {
                var itemData = await GetDoubanSubject(sid, "movie", cancellationToken).
                    ConfigureAwait(false);
                if (itemData.Subtype != "movie")
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

    }
}

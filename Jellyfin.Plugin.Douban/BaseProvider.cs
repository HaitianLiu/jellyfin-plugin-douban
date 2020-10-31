using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Douban
{
    public abstract class BaseProvider
    {
        internal const string ProviderID = "DoubanID";

        protected IHttpClient _httpClient;
        protected IJsonSerializer _jsonSerializer;
        protected ILogger _logger;

        protected Configuration.PluginConfiguration _config;
        protected DoubanAccessor _doubanAccessor;

        private const string SearchApi = "/api/v2/search/movie";

        private const string ItemApi = "/api/v2";

        protected BaseProvider(IHttpClient httpClient,
            IJsonSerializer jsonSerializer, ILogger logger)
        {
            this._httpClient = httpClient;
            this._jsonSerializer = jsonSerializer;
            this._logger = logger;
            this._config = Plugin.Instance == null ?
                new Configuration.PluginConfiguration() :
                Plugin.Instance.Configuration;

            this._doubanAccessor = new DoubanAccessor(_httpClient, _logger,
                _config.MinRequestInternalMs);
        }

        public Task<HttpResponseInfo> GetImageResponse(string url,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Douban:GetImageResponse url: {0}", url);
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken
            });
        }

        protected async Task<IEnumerable<string>> SearchSidByName(string name, string type,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Douban: Trying to search sid by name: {0}",
                name);

            var sidList = new List<string>();

            if (String.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Search name is empty.");
                return sidList;
            }

            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("q", name);
            queryParams.Add("count", "20");

            try
            {
                var response = await _doubanAccessor.Request(SearchApi, queryParams, cancellationToken);

                Response.SearchResult result = _jsonSerializer.DeserializeFromString<Response.SearchResult>(response);

                if (result.Total > 0)
                {

                    foreach (Response.SearchSubject subject in result.Items)
                    {
                        if (subject.Target_Type == type)
                        {
                            sidList.Add(subject.Target.Id);
                        }
                    }
                }
            }
            catch (HttpException e)
            {
                _logger.LogError("Could not access url: {0}, status code: {1}",
                    SearchApi, e.StatusCode);
                throw e;
            }

            return sidList;
        }

        protected async Task<MetadataResult<T>> GetMetaFromDouban<T>(string sid,
            string type, CancellationToken cancellationToken)
        where T : BaseItem, new()
        {
            _logger.LogInformation("Trying to get item by sid: {0} and type {1}", sid, type);
            var result = new MetadataResult<T>();

            if (string.IsNullOrWhiteSpace(sid))
            {
                _logger.LogWarning("Can not get movie item, sid is empty");
                return result;
            }

            var data = await GetDoubanSubject(sid, type, cancellationToken);
            if (!String.IsNullOrEmpty(type) && data.Subtype != type)
            {
                _logger.LogInformation("Douban: Sid {1}'s type is {2}, " +
                    "but require {3}", sid, data.Subtype, type);
                return result;
            }

            result.Item = TransMediaInfo<T>(data);
            TransPersonInfo(data.Directors, PersonType.Director).ForEach(result.AddPerson);
            TransPersonInfo(data.Actors, PersonType.Actor).ForEach(result.AddPerson);
            // TODO: 编剧
            // TransPersonInfo(data.Writers, PersonType.Writer).ForEach(result.AddPerson);

            result.QueriedById = true;
            result.HasMetadata = true;
            result.ResultLanguage = "zh";

            _logger.LogInformation("Douban: The name of sid {0} is {1}",
                sid, result.Item.Name);
            return result;
        }

        internal async Task<Response.Subject> GetDoubanSubject(string sid, string type,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Douban: Trying to get douban subject by " +
                "sid: {0}", sid);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sid))
            {
                throw new ArgumentException("sid is empty when getting subject");
            }

            String response = await _doubanAccessor.Request($"{ItemApi}/{type}/{sid}", new Dictionary<string, string>(), cancellationToken);

            Response.Subject subject = _jsonSerializer.DeserializeFromString<Response.Subject>(response);

            _logger.LogInformation("Get douban subject {0} successfully: {1}",
                sid, subject.Title);
            return subject;
        }

        private T TransMediaInfo<T>(Response.Subject data)
        where T : BaseItem, new()
        {
            var media = new T
            {
                Name = data.Title,
                OriginalTitle = data.Original_Title,
                CommunityRating = data.Rating?.Value,
                Overview = data.Intro.Replace("\n", "</br>"),
                ProductionYear = int.Parse(data.Year),
                HomePageUrl = data.Url,
                ProductionLocations = data.Countries?.ToArray()
            };

            if (data.Pubdate?.Count > 0 && !String.IsNullOrEmpty(data.Pubdate[0]))
            {
                string pubdate;
                if (data.Pubdate[0].IndexOf("(") != -1)
                {
                    pubdate = data.Pubdate[0].Substring(0, data.Pubdate[0].IndexOf("("));
                }
                else
                {
                    pubdate = data.Pubdate[0];
                }
                DateTime dateValue;
                if (DateTime.TryParse(pubdate, out dateValue))
                {
                    media.PremiereDate = dateValue;
                }
            }

            if (data.Trailer != null)
            {
                media.AddTrailerUrl(data.Trailer.Video_Url);
            }

            data.Genres.ForEach(media.AddGenre);

            return media;
        }

        private List<PersonInfo> TransPersonInfo(
            List<Response.PersonInfo> persons, string personType)
        {
            var result = new List<PersonInfo>();
            foreach (var person in persons)
            {
                var personInfo = new PersonInfo
                {
                    Name = person.Name,
                    Type = personType,
                    ImageUrl = person.Avatar?.Large,
                    Role = person.Roles[0]
                };

                personInfo.SetProviderId(ProviderID, person.Id);
                result.Add(personInfo);
            }
            return result;
        }
    }
}
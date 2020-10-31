using System;
using System.IO;
using System.Web;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Douban
{
    public class DoubanAccessor
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly int _minRequestInternalMs;

        private static readonly Random _random = new Random();
        // It's used to store the last access time, to reduce the access frequency.
        private static long _lastAccessTime = 0;

        // It's used to store the value of BID in cookie.
        // private static string _doubanBid = "";

        private static readonly SemaphoreSlim _locker = new SemaphoreSlim(1, 1);

        private const string UserAgent = "api-client/1 com.douban.frodo/6.42.2(194) Android/22 product/shamu vendor/OPPO model/OPPO R11 Plus  rom/android  network/wifi  platform/mobile nd/1";

        private const string ApiKey = "0dad551ec0f84ed02907ff5c42e8ec70";

        private const string SecretKey = "bf7dddc7c9cfe6f7";

        private static readonly string Host = "https://frodo.douban.com";

        public DoubanAccessor(IHttpClient client, ILogger logger) : this(client, logger, 2000) { }

        public DoubanAccessor(IHttpClient client, ILogger logger, int minRequestInternalMs)
        {
            _httpClient = client;
            _logger = logger;
            _minRequestInternalMs = minRequestInternalMs;
        }

        public async Task<String> Request(string api, Dictionary<String, String> queryParams, CancellationToken cancellationToken)
        {
            string ts = GetTs();
            StringBuilder sb = new StringBuilder();
            sb.Append("GET");
            sb.Append($"&{UpperCaseUrlEncode(api)}");
            sb.Append($"&{ts}");
            string sig = GetSig(sb.ToString());

            queryParams.Add("_ts", ts);
            queryParams.Add("_sig", sig);
            queryParams.Add("apikey", ApiKey);

            string query = GenQueryString(queryParams);

            var options = new HttpRequestOptions
            {
                Url = $"{Host}{api}{query}",
                CancellationToken = cancellationToken,
                BufferContent = true,
                UserAgent = UserAgent,
            };

            using var response = await _httpClient.GetResponse(options).ConfigureAwait(false);

            using var reader = new StreamReader(response.Content);
            String content = reader.ReadToEnd();

            return content;
        }

        // Delays for some time to reduce the access frequency.
        public async Task<String> GetResponseWithDelay(string url, Dictionary<String, String> queryParams, CancellationToken cancellationToken)
        {
            await _locker.WaitAsync();
            try
            {
                // Check the time diff to avoid high frequency, which could lead blocked by Douban.
                if (_minRequestInternalMs > 0)
                {
                    long time_diff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastAccessTime;
                    if (time_diff <= _minRequestInternalMs)
                    {
                        // Use a random delay to avoid been blocked.
                        int delay = _random.Next(_minRequestInternalMs, _minRequestInternalMs + 2000);
                        await Task.Delay(delay, cancellationToken);
                    }
                }

                var content = await Request(url, queryParams, cancellationToken);
                // Update last access time to now.
                _lastAccessTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return content;
            }
            finally
            {
                _locker.Release();
            }
        }

        private string GetTs()
        {
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000;
            return $"{ts}";
        }

        private string GetSig(string input)
        {
            using var hmac1 = new HMACSHA1(Encoding.UTF8.GetBytes(SecretKey));
            hmac1.Initialize();
            byte[] data = hmac1.ComputeHash(Encoding.UTF8.GetBytes(input));

            return Convert.ToBase64String(data);
        }

        private static string UpperCaseUrlEncode(string s)
        {
            char[] temp = HttpUtility.UrlEncode(s).ToCharArray();
            for (int i = 0; i < temp.Length - 2; i++)
            {
                if (temp[i] == '%')
                {
                    temp[i + 1] = char.ToUpper(temp[i + 1]);
                    temp[i + 2] = char.ToUpper(temp[i + 2]);
                }
            }
            return new string(temp);
        }

        private static string GenQueryString(Dictionary<string, string> queryParams)
        {
            // StringBuilder sb = new StringBuilder("?");
            List<string> temp = new List<string>();
            foreach (KeyValuePair<string, string> entry in queryParams)
            {
                temp.Add($"{UpperCaseUrlEncode(entry.Key)}={UpperCaseUrlEncode(entry.Value)}");
            }
            return "?" + String.Join("&", temp);
        }
    }
}
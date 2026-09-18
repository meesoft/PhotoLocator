using MapControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    public sealed class NominatimLocationSearcher
    {
        static readonly Uri _searchEndpoint = new("https://nominatim.openstreetmap.org/search");
        static HttpClient? _sharedHttpClient;

        static HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PhotoLocator");
            return httpClient;
        }
        
        readonly HttpClient _httpClient;

        public NominatimLocationSearcher(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? (_sharedHttpClient ??= CreateHttpClient());
        }

        public async Task<IReadOnlyList<LocationWithName>> SearchAsync(string searchText, int resultLimit = 10, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(searchText);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resultLimit);

            var requestUri = new UriBuilder(_searchEndpoint)
            {
                Query = $"q={Uri.EscapeDataString(searchText)}&format=jsonv2&limit={resultLimit}"
            }.Uri;

            var results = await _httpClient.GetFromJsonAsync<NominatimResult[]>(requestUri, cancellationToken)
                .ConfigureAwait(false);

            if (results is null)
                return [];

            return results
                .Where(result =>
                    result.DisplayName is not null &&
                    double.TryParse(result.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                    double.TryParse(result.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .Select(result => new LocationWithName(
                    result.DisplayName!,
                    new Location(
                        double.Parse(result.Latitude!, CultureInfo.InvariantCulture),
                        double.Parse(result.Longitude!, CultureInfo.InvariantCulture))))
                .ToArray();
        }

        sealed class NominatimResult
        {
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }

            [JsonPropertyName("lat")]
            public string? Latitude { get; set; }

            [JsonPropertyName("lon")]
            public string? Longitude { get; set; }
        }
    }

    public sealed record LocationWithName(string DisplayName, Location Location);
}

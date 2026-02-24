using System.Net;
using System.Text;
using System.Text.Json;
using FFXIV.Infrastructure.ExternalApis;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.Market;

public class UniversalisClientTests
{
    private readonly ILogger<UniversalisClient> _logger = Substitute.For<ILogger<UniversalisClient>>();

    private static HttpClient CreateHttpClientWithHandler(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://universalis.app/api/v2/")
        };
        return client;
    }

    [Fact]
    public async Task GetItemPricesAsync_ValidResponse_ReturnsMappedData()
    {
        // Arrange
        var json = """
        {
            "itemID": 5111,
            "listings": [
                {
                    "pricePerUnit": 500,
                    "quantity": 10,
                    "total": 5000,
                    "hq": false,
                    "retainerName": "TestRetainer"
                }
            ],
            "averagePrice": 450,
            "averagePriceNQ": 400,
            "averagePriceHQ": 600,
            "minPrice": 350,
            "minPriceNQ": 300,
            "minPriceHQ": 500,
            "listingCount": 15,
            "lastUploadTime": 1700000000000
        }
        """;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesAsync("Adamantoise", 5111);

        // Assert
        result.Should().NotBeNull();
        result!.ItemId.Should().Be(5111);
        result.AveragePriceNQ.Should().Be(400);
        result.AveragePriceHQ.Should().Be(600);
        result.MinPriceNQ.Should().Be(300);
        result.MinPriceHQ.Should().Be(500);
        result.CurrentListingCount.Should().Be(15);
        result.RecentListings.Should().HaveCount(1);
        result.RecentListings[0].PricePerUnit.Should().Be(500);
        result.RecentListings[0].IsHQ.Should().BeFalse();
        result.RecentListings[0].RetainerName.Should().Be("TestRetainer");
    }

    [Fact]
    public async Task GetItemPricesAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, "");
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesAsync("Adamantoise", 99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetItemPricesBatchAsync_MultipleItems_ReturnsMappedData()
    {
        // Arrange
        var json = """
        {
            "items": {
                "5111": {
                    "itemID": 5111,
                    "listings": [],
                    "averagePrice": 100,
                    "averagePriceNQ": 90,
                    "averagePriceHQ": 150,
                    "minPrice": 80,
                    "minPriceNQ": 70,
                    "minPriceHQ": 120,
                    "listingCount": 5,
                    "lastUploadTime": 1700000000000
                },
                "5112": {
                    "itemID": 5112,
                    "listings": [],
                    "averagePrice": 200,
                    "averagePriceNQ": 180,
                    "averagePriceHQ": 250,
                    "minPrice": 160,
                    "minPriceNQ": 150,
                    "minPriceHQ": 220,
                    "listingCount": 3,
                    "lastUploadTime": 1700000000000
                }
            }
        }
        """;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesBatchAsync("Aether", [5111, 5112]);

        // Assert
        result.Should().HaveCount(2);
        result[5111].AveragePriceNQ.Should().Be(90);
        result[5112].AveragePriceNQ.Should().Be(180);
    }

    [Fact]
    public async Task GetItemPricesBatchAsync_SingleItem_ParsesSingleResponse()
    {
        // Arrange: single item returns flat response, not items dict
        var json = """
        {
            "itemID": 5111,
            "listings": [],
            "averagePrice": 100,
            "averagePriceNQ": 90,
            "averagePriceHQ": 150,
            "minPrice": 80,
            "minPriceNQ": 70,
            "minPriceHQ": 120,
            "listingCount": 5,
            "lastUploadTime": 1700000000000
        }
        """;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesBatchAsync("Aether", [5111]);

        // Assert
        result.Should().HaveCount(1);
        result[5111].AveragePriceNQ.Should().Be(90);
    }

    [Fact]
    public async Task GetItemPricesBatchAsync_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesBatchAsync("Aether", []);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetItemPricesBatchAsync_Over100Items_ChunksRequests()
    {
        // Arrange: build 150 items, expect 2 requests
        var items = new Dictionary<string, object>();
        for (var i = 1; i <= 100; i++)
        {
            items[i.ToString()] = new
            {
                itemID = i, listings = Array.Empty<object>(),
                averagePrice = 100, averagePriceNQ = 90, averagePriceHQ = 150,
                minPrice = 80, minPriceNQ = 70, minPriceHQ = 120,
                listingCount = 5, lastUploadTime = 1700000000000L
            };
        }
        var batch1Json = JsonSerializer.Serialize(new { items });

        var items2 = new Dictionary<string, object>();
        for (var i = 101; i <= 150; i++)
        {
            items2[i.ToString()] = new
            {
                itemID = i, listings = Array.Empty<object>(),
                averagePrice = 200, averagePriceNQ = 180, averagePriceHQ = 250,
                minPrice = 160, minPriceNQ = 150, minPriceHQ = 220,
                listingCount = 3, lastUploadTime = 1700000000000L
            };
        }
        var batch2Json = JsonSerializer.Serialize(new { items });

        var handler = new MultiResponseHttpHandler([
            (HttpStatusCode.OK, batch1Json),
            (HttpStatusCode.OK, batch2Json)
        ]);
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var itemIds = Enumerable.Range(1, 150).ToList();
        var result = await client.GetItemPricesBatchAsync("Aether", itemIds);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(100);
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task GetItemPricesAsync_StaleData_IsStaleReturnsTrue()
    {
        // Arrange: set lastUploadTime to 48 hours ago
        var staleTime = DateTimeOffset.UtcNow.AddHours(-48).ToUnixTimeMilliseconds();
        var json = $$"""
        {
            "itemID": 5111,
            "listings": [],
            "averagePrice": 100,
            "averagePriceNQ": 90,
            "averagePriceHQ": 150,
            "minPrice": 80,
            "minPriceNQ": 70,
            "minPriceHQ": 120,
            "listingCount": 5,
            "lastUploadTime": {{staleTime}}
        }
        """;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesAsync("Adamantoise", 5111);

        // Assert
        result.Should().NotBeNull();
        result!.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task GetItemPricesAsync_RecentData_IsStaleReturnsFalse()
    {
        // Arrange: set lastUploadTime to 1 hour ago
        var recentTime = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
        var json = $$"""
        {
            "itemID": 5111,
            "listings": [],
            "averagePrice": 100,
            "averagePriceNQ": 90,
            "averagePriceHQ": 150,
            "minPrice": 80,
            "minPriceNQ": 70,
            "minPriceHQ": 120,
            "listingCount": 5,
            "lastUploadTime": {{recentTime}}
        }
        """;
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var client = new UniversalisClient(CreateHttpClientWithHandler(handler), _logger);

        // Act
        var result = await client.GetItemPricesAsync("Adamantoise", 5111);

        // Assert
        result.Should().NotBeNull();
        result!.IsStale.Should().BeFalse();
    }

    /// <summary>
    /// Fake handler that always returns the same response.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Fake handler that returns different responses for sequential requests.
    /// </summary>
    private sealed class MultiResponseHttpHandler : HttpMessageHandler
    {
        private readonly List<(HttpStatusCode Status, string Content)> _responses;
        private int _requestIndex;

        public int RequestCount => _requestIndex;

        public MultiResponseHttpHandler(List<(HttpStatusCode, string)> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, content) = _requestIndex < _responses.Count
                ? _responses[_requestIndex]
                : (HttpStatusCode.InternalServerError, "");

            _requestIndex++;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}

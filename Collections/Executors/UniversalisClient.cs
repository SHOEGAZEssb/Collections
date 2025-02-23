using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Collections;

public class UniversalisClient
{
    public Dictionary<uint, MarketplaceItemData> itemToMarketplaceData = new();
    private const string Fields = "listings.pricePerUnit,averagePriceNQ,averagePriceHQ";

    private readonly HttpClient httpClient =
    new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromMilliseconds(10000)
    };

    private World homeWorld = Services.ClientState.LocalPlayer?.CurrentWorld.GameData;

    public void Dispose()
    {
        httpClient.Dispose();
    }
    public async Task populateMarketBoardData(uint itemId)
    {
        if (itemToMarketplaceData.ContainsKey(itemId))
        {
            return;
        }
        itemToMarketplaceData[itemId] = await GetMarketBoardData(itemId).ConfigureAwait(false);
    }
    public async Task<MarketplaceItemData?> GetMarketBoardData(uint itemId)
    {
        homeWorld ??= Services.ClientState.LocalPlayer?.CurrentWorld.GameData;
        var worldData = await GetMarketBoardDataInternal(itemId, homeWorld.Name);
        var DCData = await GetMarketBoardDataInternal(itemId, homeWorld.DataCenter.Value.Name);
        return ParseMarketplaceItemData(worldData, DCData);
    }

    private async Task<UniversalisItemData?> GetMarketBoardDataInternal(uint itemId, string worldDcRegion)
    {
        try
        {
            using var result = await httpClient.GetAsync($"https://universalis.app/api/v2/{worldDcRegion}/{itemId}?Fields={Fields}");

            if (result.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            await using var responseStream = await result.Content.ReadAsStreamAsync();
            var item = await JsonSerializer.DeserializeAsync<UniversalisItemData>(responseStream);
            if (item == null)
            {
                return null;
            }

            return item;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private MarketplaceItemData ParseMarketplaceItemData(UniversalisItemData worldData, UniversalisItemData DCData)
    {
        return new MarketplaceItemData()
        {
            minPriceWorld = worldData.listings.FirstOrDefault()?.pricePerUnit,
            minPriceDC = DCData.listings.FirstOrDefault()?.pricePerUnit,
            avgPriceWorld = Math.Min(worldData.averagePriceNQ, worldData.averagePriceHQ),
            avgPriceDC = Math.Min(DCData.averagePriceNQ, DCData.averagePriceHQ),
        };
    }

    public class MarketplaceItemData
    {
        public double? minPriceWorld;
        public double avgPriceWorld;
        public double? minPriceDC;
        public double avgPriceDC;
    }

    private class UniversalisItemData
    {
        public List<Listing> listings { get; set; }
        public double averagePriceNQ { get; set; }
        public double averagePriceHQ { get; set; }

        public class Listing
        {
            public long pricePerUnit { get; set; }
        }
    }
}

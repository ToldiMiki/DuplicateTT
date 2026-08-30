using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SmartPageDuplicate;
using SmartPageDuplicate.Models;
using Xunit;

namespace SmartPageDuplicate.Tests;

/// <summary>
/// A modellek oda-vissza alakításának tesztjei. Ez az a pont, ahol a csendes adatvesztések
/// keletkeztek: ha egy mezőt a modell nem ismer, a beolvasás eldobja, a mentés pedig már nem
/// küldi vissza - a másolat némán eltér az eredetitől.
///
/// A JSON-részletek a PROD2 szerver valós válaszaiból származnak.
/// </summary>
public class ModelRoundTripTests
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ---------------------------------------------------------------- H2

    [Fact]
    public void H2_ACellaKesesKuszobeAtmegyAMasolatba()
    {
        // A BKK v3 8sor menetrend celláin a delayThreshold 5. A modell korábban nem ismerte,
        // így a másolatban a szerver alapértéke (0) lépett a helyébe - a másolt tábla máskor
        // jelzett késést, mint az eredeti.
        const string json = """
        {
          "id": 13264, "dynamicTimeRowId": 10201, "x": 10, "y": 32,
          "width": 100, "height": 96, "anchorX": "CENTER", "anchorY": "MIDDLE",
          "type": "ICON_VEHICLE_TYPE", "textValue": "ˬ", "cellOrder": 1,
          "backgroundColor": "FFF_White", "fontColor": "_000_Black",
          "rasterFontId": 10136, "ttFontName": "OpenSans-Bold + icons",
          "countdownLimitNear": 30, "exactTimeperiod": 9999, "delayThreshold": 5
        }
        """;

        var cell = JsonSerializer.Deserialize<DynamicCell>(json, ReadOptions);
        Assert.NotNull(cell);
        Assert.Equal(5, cell!.DelayThreshold);

        var written = JsonSerializer.SerializeToNode(cell, WriteOptions);
        Assert.Equal(5, written!["delayThreshold"]!.GetValue<int>());
    }

    // ---------------------------------------------------------------- H1

    [Fact]
    public void H1_AKozlemenyElemeiAtmennekAMasolatba()
    {
        // A szerver "items" néven küldi a közlemény elemeit. A modell korábban
        // "textAnnouncements" és "imageAnnouncements" mezőt várt - ilyet a szerver nem ad,
        // így az items tartalma eldobódott.
        const string json = """
        {
          "id": 10003, "name": "szöveg 1sor 1100px 40kar BW BarlowB32",
          "description": "szöveges közlemény", "defaultText": "Ez egy 1 soros szöveges közlemény!",
          "items": [ { "id": 1, "text": "első" }, { "id": 2, "text": "második" } ],
          "groupIds": [10004]
        }
        """;

        var announcement = JsonSerializer.Deserialize<Announcement>(json, ReadOptions);
        Assert.NotNull(announcement);
        Assert.NotNull(announcement!.Items);
        Assert.Equal(2, announcement.Items!.Count);

        var written = JsonSerializer.SerializeToNode(announcement, WriteOptions);
        Assert.Equal(2, written!["items"]!.AsArray().Count);
        Assert.Equal("Ez egy 1 soros szöveges közlemény!", written["defaultText"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------- mezőőr

    [Fact]
    public void AMezoorEszreveszAModellAltalNemIsmertMezot()
    {
        // Ez az egyetlen védelem a következő H1/H2 ellen: az API-nak nincs dokumentációja,
        // tehát egy új mező megjelenését semmi más nem jelezné.
        const string json = """
        { "id": 1, "name": "teszt", "width": 1200, "height": 800, "valamiUjMezo": 42 }
        """;

        var unknown = ModelFieldGuard.FindUnknownFields(JsonNode.Parse(json), typeof(TimetableItem));

        Assert.Contains("valamiUjMezo", unknown);
    }

    [Fact]
    public void AMezoorABeagyazottSzintekenIsKeres()
    {
        const string json = """
        {
          "id": 1, "name": "teszt", "width": 1200, "height": 800,
          "dynamicRows": [ { "id": 2, "rowOrder": 1,
            "dynamicCells": [ { "id": 3, "cellOrder": 1, "ujCellaMezo": true } ] } ]
        }
        """;

        var unknown = ModelFieldGuard.FindUnknownFields(JsonNode.Parse(json), typeof(TimetableItem));

        Assert.Contains("dynamicRows[].dynamicCells[].ujCellaMezo", unknown);
    }

    [Fact]
    public void AMezoorNemJelezHaMindenMezotIsmerAModell()
    {
        const string json = """
        {
          "id": 10028, "name": "BKK v3 8sor (176px)", "width": 1200, "height": 1408,
          "x": 0, "y": 96, "scrollbarWidth": 0, "imageId": 10706,
          "imageWidth": 1200, "imageHeight": 1600, "groupIds": [10010],
          "dynamicRows": [ { "id": 10201, "dynamicTimeTableId": 10028, "x": 0, "y": 0,
            "width": 1200, "height": 176, "rowOrder": 1, "dynamicCells": [] } ]
        }
        """;

        var unknown = ModelFieldGuard.FindUnknownFields(JsonNode.Parse(json), typeof(TimetableItem));

        Assert.Empty(unknown);
    }

    [Fact]
    public void AMezoorUgyanaztAMezotCsakEgyszerJelenti()
    {
        // A hiányzó mező minden során és celláján megjelenne - egyszer érdemes jelenteni.
        const string json = """
        {
          "id": 1, "name": "t", "width": 1, "height": 1,
          "dynamicRows": [
            { "id": 2, "rowOrder": 1, "dynamicCells": [ { "id": 3, "ujMezo": 1 }, { "id": 4, "ujMezo": 2 } ] },
            { "id": 5, "rowOrder": 2, "dynamicCells": [ { "id": 6, "ujMezo": 3 } ] }
          ]
        }
        """;

        var unknown = ModelFieldGuard.FindUnknownFields(JsonNode.Parse(json), typeof(TimetableItem));

        Assert.Single(unknown);
    }
}

using System.Text.Json.Nodes;
using SmartPageDuplicate.Copy;
using Xunit;

namespace SmartPageDuplicate.Tests;

/// <summary>
/// A fordítási logika tesztjei. Minden teszt egy konkrét, a PROD2 homokozón mért hibához
/// tartozik - a javítás visszavonásakor pirosra kell váltania.
///
/// A használt azonosítók valós PROD2 értékek: a csoportok 10004-től, az elemtípusok 10019-től,
/// az AnchorX 10042-től, a TextColor 10060-tól indul.
/// </summary>
public class CopyTranslatorTests
{
    private static ServerCatalog Source() => new()
    {
        ServerKey = "PROD",
        Displays = { new NamedEntity(10014, "13inch 1200x1600 gray16") },
        Groups = { new NamedEntity(10004, "Tüke"), new NamedEntity(10010, "BKK") },
        Images = { new NamedEntity(10156, "Z_ETA 3x8") },
        Grids = { new NamedEntity(10032, "Menetrend 3x8") },
        Timetables = { new NamedEntity(10028, "BKK v3 8sor (176px)") },
        Stops = { new NamedEntity(10080, "Csontváry utca (Belváros felé)") },
        States = { new NamedEntity(10019, "Minden nap, normál akku") },
        RasterFonts = { new RasterFontInfo(10054, "BarlowBold", 32) },
        ElementTypes = { [10019] = "ImageDisplayNormal", [10021] = "Clock" },
        AnchorX = { [10042] = "LEFT", [10043] = "CENTER" },
        AnchorY = { [10045] = "TOP" },
        TextColors = { [10060] = "FFF_White", [10061] = "000_Black" },
    };

    /// <summary>Ugyanazok a nevek, de mindenütt eltérő azonosítókkal - ez a fordítás lényege.</summary>
    private static ServerCatalog Target() => new()
    {
        ServerKey = "DEMO",
        Displays = { new NamedEntity(20014, "13inch 1200x1600 gray16") },
        Groups = { new NamedEntity(20004, "Tüke"), new NamedEntity(20010, "BKK") },
        Images = { new NamedEntity(20156, "Z_ETA 3x8") },
        Grids = { new NamedEntity(20032, "Menetrend 3x8") },
        Timetables = { new NamedEntity(20028, "BKK v3 8sor (176px)") },
        Stops = { new NamedEntity(20080, "Csontváry utca (Belváros felé)") },
        States = { new NamedEntity(20019, "Minden nap, normál akku") },
        RasterFonts = { new RasterFontInfo(20054, "BarlowBold", 32) },
        ElementTypes = { [20019] = "ImageDisplayNormal", [20021] = "Clock" },
        AnchorX = { [20042] = "LEFT", [20043] = "CENTER" },
        AnchorY = { [20045] = "TOP" },
        TextColors = { [20060] = "FFF_White", [20061] = "000_Black" },
    };

    private static CopyTranslator CrossServer() => new(Source(), Target(), isSameServer: false);
    private static CopyTranslator SameServer() => new(Source(), Source(), isSameServer: true);

    private static JsonObject Element(params (string Key, JsonNode? Value)[] fields)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in fields) obj[key] = value;
        return obj;
    }

    // ---------------------------------------------------------------- H3

    [Fact]
    public void H3_RaszterfontAzonositoTenylegesenAtirodik()
    {
        // A hiba: az értékadás egy "is JsonObject" feltétel mögött állt, ami számértéknél
        // sosem teljesül, így a fordítás eredménye soha nem íródott vissza.
        var translator = CrossServer();
        var item = Element(("rasterFontId", 10054), ("name", "óra"));

        Assert.True(translator.TranslateLayoutElement(item, "óra"));
        Assert.Equal(20054, item["rasterFontId"]!.GetValue<int>());
    }

    [Fact]
    public void H3_MenetrendCellajanakFontjaIsAtirodik()
    {
        var translator = CrossServer();
        var timetable = new JsonObject
        {
            ["dynamicRows"] = new JsonArray
            {
                new JsonObject { ["dynamicCells"] = new JsonArray { Element(("rasterFontId", 10054)) } }
            }
        };

        translator.TranslateTimetable(timetable);

        int translated = timetable["dynamicRows"]![0]!["dynamicCells"]![0]!["rasterFontId"]!.GetValue<int>();
        Assert.Equal(20054, translated);
    }

    // ---------------------------------------------------------------- H4

    [Theory]
    [InlineData("gridId", 10032, 20032)]
    [InlineData("dynamicTimetableId", 10028, 20028)]
    public void H4_KorabbanForditatlanHivatkozasokAtirodnak(string field, int sourceId, int expected)
    {
        var translator = CrossServer();
        var item = Element((field, sourceId));

        Assert.True(translator.TranslateLayoutElement(item, "elem"));
        Assert.Equal(expected, item[field]!.GetValue<int>());
    }

    [Fact]
    public void H4_KepAzonositoAtirodikHaANevMegvanACelon()
    {
        var translator = CrossServer();
        var item = Element(("imageId", 10156));

        var lookup = translator.LookupImage(item, "imageId", "kép elem");

        Assert.Equal(CopyTranslator.ImageLookupKind.FoundOnTarget, lookup.Kind);
        Assert.Equal(20156, item["imageId"]!.GetValue<int>());
    }

    [Fact]
    public void H4_HianyzoKepFeltoltestJelez()
    {
        var target = Target();
        target.Images.Clear();
        var translator = new CopyTranslator(Source(), target, isSameServer: false);
        var item = Element(("imageId", 10156));

        var lookup = translator.LookupImage(item, "imageId", "kép elem");

        Assert.Equal(CopyTranslator.ImageLookupKind.NeedsUpload, lookup.Kind);
        Assert.Equal("Z_ETA 3x8", lookup.Name);
        // A mező érintetlen marad, amíg a feltöltés meg nem történt.
        Assert.Equal(10156, item["imageId"]!.GetValue<int>());
    }

    [Fact]
    public void FeltoltottKepBekerulANevtablabaHogyNeToltodjonFelKetszer()
    {
        var target = Target();
        target.Images.Clear();
        var translator = new CopyTranslator(Source(), target, isSameServer: false);

        var first = Element(("imageId", 10156));
        Assert.Equal(CopyTranslator.ImageLookupKind.NeedsUpload,
            translator.LookupImage(first, "imageId", "első").Kind);
        translator.RegisterUploadedImage(first, "imageId", 30156, "Z_ETA 3x8");

        var second = Element(("imageId", 10156));
        var lookup = translator.LookupImage(second, "imageId", "második");

        Assert.Equal(CopyTranslator.ImageLookupKind.FoundOnTarget, lookup.Kind);
        Assert.Equal(30156, second["imageId"]!.GetValue<int>());
    }

    // ---------------------------------------------------------------- H27

    [Fact]
    public void H27_ANevparositasNemBukikElZaroSzokozon()
    {
        // A PROD-on a "SourceSans3-Bold " családnév záró szóközzel szerepel, a DEMO-n anélkül.
        var source = Source();
        source.RasterFonts.Clear();
        source.RasterFonts.Add(new RasterFontInfo(10099, "SourceSans3-Bold ", 28));

        var target = Target();
        target.RasterFonts.Clear();
        target.RasterFonts.Add(new RasterFontInfo(20099, "SourceSans3-Bold", 28));

        var translator = new CopyTranslator(source, target, isSameServer: false);
        var item = Element(("rasterFontId", 10099));

        Assert.True(translator.TranslateLayoutElement(item, "szöveg"));
        Assert.Equal(20099, item["rasterFontId"]!.GetValue<int>());
    }

    // ---------------------------------------------------------------- H8

    [Fact]
    public void H8_ANullasAzonositoErvenyesTalalatNemHianyzoErtek()
    {
        // A régi kód a 0-s kulcsot vette "nincs találat" jelzésnek. Ma minden azonosító 10000
        // felett van, de ez a telepítés véletlene, nem az API szerződése.
        var source = Source();
        source.ElementTypes.Clear();
        source.ElementTypes[0] = "ImageDisplayNormal";

        var target = Target();
        target.ElementTypes.Clear();
        target.ElementTypes[0] = "ImageDisplayNormal";

        var translator = new CopyTranslator(source, target, isSameServer: false);
        var item = Element(("elementTypeId", 0));

        Assert.True(translator.TranslateLayoutElement(item, "kép"));
        Assert.Equal(0, item["elementTypeId"]!.GetValue<int>());
        Assert.Empty(translator.Report.Skipped);
    }

    // ---------------------------------------------------------------- H18

    [Fact]
    public void H18_AFaModositasaIteracioKozbenNemBorulFel()
    {
        // A fordítás ugyanazt az objektumot írja, amin a bejárás fut - ezért gyűjt előbb,
        // és módosít csak utána.
        var translator = CrossServer();
        var timetable = new JsonObject
        {
            ["id"] = 10000,
            ["name"] = "menetrend",
            ["imageId"] = 10156,
            ["imageContent"] = "base64...",
            ["groupIds"] = new JsonArray { 10004 },
            ["dynamicRows"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = 10201,
                    ["dynamicTimeTableId"] = 10000,
                    ["dynamicCells"] = new JsonArray { Element(("id", 13264), ("rasterFontId", 10054)) }
                }
            }
        };

        var exception = Record.Exception(() => translator.TranslateTimetable(timetable));

        Assert.Null(exception);
        Assert.Null(timetable["id"]);              // az azonosítók törlődnek
        Assert.Null(timetable["imageContent"]);    // a base64 tartalom is
        Assert.NotNull(timetable["imageId"]);      // a háttérképet a hívó kezeli
        Assert.Equal(20004, timetable["groupIds"]![0]!.GetValue<int>());
    }

    // ---------------------------------------------------------------- azonos szerver

    [Fact]
    public void AzonosSzerverenBelulAzAzonositokValtozatlanokMaradnak()
    {
        var translator = SameServer();
        var item = Element(("gridId", 10032), ("imageId", 10156), ("rasterFontId", 10054));

        Assert.True(translator.TranslateLayoutElement(item, "elem"));
        Assert.Equal(10032, item["gridId"]!.GetValue<int>());
        Assert.Equal(10054, item["rasterFontId"]!.GetValue<int>());
        Assert.Equal(CopyTranslator.ImageLookupKind.NothingToDo,
            translator.LookupImage(item, "imageId", "elem").Kind);
        Assert.Empty(translator.Report.Conversions);
    }

    // ---------------------------------------------------------------- jogosultsági csoportok

    [Fact]
    public void HianyzoJogosultsagiCsoportKimaradDeATobbiAtmegy()
    {
        var target = Target();
        target.Groups.RemoveAll(g => g.Name == "BKK");
        var translator = new CopyTranslator(Source(), target, isSameServer: false);

        var header = new JsonObject { ["groupIds"] = new JsonArray { 10004, 10010 } };
        translator.TranslateLayoutHeader(header);

        var groups = (JsonArray)header["groupIds"]!;
        Assert.Single(groups);
        Assert.Equal(20004, groups[0]!.GetValue<int>());
        Assert.Contains(translator.Report.Skipped, s => s.Contains("BKK"));
    }

    // ---------------------------------------------------------------- blokkoló hiány

    [Fact]
    public void HianyzoRaszterfontAMenetrendbenBlokkoloHiba()
    {
        // A fontok API-ból nem vihetők át, és a cella font nélkül nem menthető.
        var target = Target();
        target.RasterFonts.Clear();
        var translator = new CopyTranslator(Source(), target, isSameServer: false);

        var timetable = new JsonObject
        {
            ["dynamicRows"] = new JsonArray
            {
                new JsonObject { ["dynamicCells"] = new JsonArray { Element(("rasterFontId", 10054)) } }
            }
        };
        translator.TranslateTimetable(timetable);

        Assert.True(translator.Report.HasBlockingProblems);
        Assert.Contains(translator.Report.Blocking, b => b.Contains("BarlowBold") && b.Contains("32"));
    }

    [Fact]
    public void HianyzoRaszterfontALayoutElemenCsakAztAzElemetEjtiKi()
    {
        var target = Target();
        target.RasterFonts.Clear();
        var translator = new CopyTranslator(Source(), target, isSameServer: false);

        Assert.False(translator.TranslateLayoutElement(Element(("rasterFontId", 10054)), "óra"));
        Assert.False(translator.Report.HasBlockingProblems);
        Assert.Contains(translator.Report.Skipped, s => s.Contains("BarlowBold"));
    }

    [Fact]
    public void HianyzoKijelzoBlokkoloHiba()
    {
        var target = Target();
        target.Displays.Clear();
        var translator = new CopyTranslator(Source(), target, isSameServer: false);

        Assert.False(translator.TranslateLayoutHeader(new JsonObject { ["displayId"] = 10014 }));
        Assert.True(translator.Report.HasBlockingProblems);
    }

    // ---------------------------------------------------------------- megálló-kötés

    [Fact]
    public void MegalloKotesForditasaNevSzerint()
    {
        var translator = CrossServer();
        var slide = new JsonObject
        {
            ["id"] = 10140,
            ["stopId"] = 10080,
            ["stateId"] = 10019,
            ["prioritySn"] = 1,
            ["timer"] = 60,
            ["informationSlide"] = true,
            ["description"] = "Minden nap, normál akku szint"
        };

        var result = translator.TranslateSlide(slide, newLayoutId: 30001);

        Assert.NotNull(result);
        Assert.Equal(30001, result!["layoutId"]!.GetValue<int>());
        Assert.Equal(20080, result["stopId"]!.GetValue<int>());
        Assert.Equal(20019, result["stateId"]!.GetValue<int>());
        Assert.Equal(1, result["prioritySn"]!.GetValue<int>());
        Assert.Equal(60, result["timer"]!.GetValue<int>());
        Assert.True(result["informationSlide"]!.GetValue<bool>());
        Assert.Null(result["id"]);   // az eredeti azonosító nem mehet át
    }

    [Fact]
    public void MegalloKotesKimaradHaAMegalloNincsMegACelon()
    {
        var target = Target();
        target.Stops.Clear();
        var translator = new CopyTranslator(Source(), target, isSameServer: false);

        var slide = new JsonObject { ["stopId"] = 10080, ["stateId"] = 10019 };

        Assert.Null(translator.TranslateSlide(slide, 30001));
        Assert.Contains(translator.Report.Skipped, s => s.Contains("Csontváry"));
    }

    // ---------------------------------------------------------------- címkék

    [Fact]
    public void ASzarmaztatottCimkekNemMennekAtAMasolatba()
    {
        // Ezeket a szerver az azonosítóból származtatja; visszaküldve félrevezetőek lennének.
        var translator = CrossServer();
        var item = Element(
            ("elementTypeId", 10019), ("elementTypeLabel", "ImageDisplayNormal"),
            ("anchorX", 10042), ("anchorXLabel", "LEFT"),
            ("anchorY", 10045), ("anchorYLabel", "TOP"),
            ("rasterFontId", 10054), ("ttFontName", "BarlowBold"));

        Assert.True(translator.TranslateLayoutElement(item, "elem"));
        Assert.Null(item["elementTypeLabel"]);
        Assert.Null(item["anchorXLabel"]);
        Assert.Null(item["anchorYLabel"]);
        Assert.Null(item["ttFontName"]);
        Assert.Equal(20019, item["elementTypeId"]!.GetValue<int>());
        Assert.Equal(20042, item["anchorX"]!.GetValue<int>());
    }

    [Fact]
    public void AJelentesTipusonkentSzamoljaAValodiForditasokat()
    {
        var translator = CrossServer();
        translator.TranslateLayoutElement(
            Element(("elementTypeId", 10019), ("anchorX", 10042), ("gridId", 10032)), "elem");

        Assert.Equal(1, translator.Report.Conversions["elemtípus"]);
        Assert.Equal(1, translator.Report.Conversions["AnchorX érték"]);
        Assert.Equal(1, translator.Report.Conversions["rács"]);
    }
}

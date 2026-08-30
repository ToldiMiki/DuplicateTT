using System.Collections.Generic;

namespace SmartpageTimetableDuplicateV1.Copy
{
    /// <summary>Név szerint azonosítható elem (kép, rács, menetrend, megálló, állapot, kijelző, csoport).</summary>
    public record NamedEntity(int Id, string Name);

    /// <summary>A raszterfontot a családnév és a méret párosa azonosítja, nem a neve önmagában.</summary>
    public record RasterFontInfo(int Id, string TtFontName, int Size);

    /// <summary>
    /// Egy szerver névtáblái: minden, amire egy másolat hivatkozhat. A szerverek között az
    /// azonosítók nem hordozhatók, a nevek igen - a fordítás ezeken a táblákon dolgozik.
    /// </summary>
    public class ServerCatalog
    {
        public string ServerKey { get; init; } = "";

        public List<NamedEntity> Displays { get; init; } = new();
        public List<NamedEntity> Groups { get; init; } = new();
        public List<NamedEntity> Images { get; init; } = new();
        public List<NamedEntity> Grids { get; init; } = new();
        public List<NamedEntity> Timetables { get; init; } = new();
        public List<NamedEntity> Stops { get; init; } = new();
        public List<NamedEntity> States { get; init; } = new();
        public List<RasterFontInfo> RasterFonts { get; init; } = new();

        // Az enum jellegű táblák azonosító -> címke alakban érkeznek.
        public Dictionary<int, string> ElementTypes { get; init; } = new();
        public Dictionary<int, string> AnchorX { get; init; } = new();
        public Dictionary<int, string> AnchorY { get; init; } = new();
        public Dictionary<int, string> TextColors { get; init; } = new();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace SmartPageDuplicate.Copy
{
    /// <summary>
    /// A másolás fordítási logikája: a forrás szerver azonosítóit a cél szerver azonosítóira
    /// írja át, a nevek alapján. Nem ismer sem felületet, sem hálózatot - bemenet a nyers JSON
    /// és a két névtábla, kimenet a módosított JSON és egy jelentés. Így egyben tesztelhető,
    /// és éppen ez a rész az, ahol a csendes adatvesztések keletkeztek.
    ///
    /// Egy kivétel van: a kép feltöltése hálózatot igényel, ezért azt a hívó végzi - a fordító
    /// csak megmondja, mit talált (<see cref="LookupImage"/>).
    /// </summary>
    public class CopyTranslator
    {
        private readonly ServerCatalog _source;
        private readonly ServerCatalog _target;

        /// <summary>Egy szerveren belül az eredeti azonosítók a helyesek - ilyenkor nem fordítunk.</summary>
        public bool IsSameServer { get; }

        public TranslationReport Report { get; } = new();

        public CopyTranslator(ServerCatalog source, ServerCatalog target, bool isSameServer)
        {
            _source = source;
            _target = target;
            IsSameServer = isSameServer;
        }

        /// <summary>
        /// A szerverek között a nevek stabilak, de nem mindig karakterre pontosan: a PROD-on
        /// például a "SourceSans3-Bold " fontcsalád neve záró szóközzel szerepel, a DEMO-n
        /// anélkül. Minden név szerinti párosítás ezen a normalizáláson megy át.
        /// </summary>
        public static bool NameEquals(string? a, string? b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- menetrend

        /// <summary>
        /// A menetrend fájá járása: azonosítók törlése és fordítása. A háttérkép (imageId)
        /// érintetlen marad - azt a hívó kezeli, mert feltöltéssel járhat.
        /// </summary>
        public void TranslateTimetable(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                // Előbb csak besorolunk, módosítani csak a ciklus után szabad: az értékcsere
                // ugyanazt az objektumot írja, amin a foreach fut.
                var toRemove = new List<string>();
                var groupIdsKeys = new List<string>();
                var rasterFontKeys = new List<string>();

                foreach (var kv in obj)
                {
                    string name = kv.Key;
                    if (name.Equals("imageId", StringComparison.OrdinalIgnoreCase))
                    {
                        // A hívó kezeli (név szerinti keresés, szükség esetén feltöltés).
                    }
                    else if (name.Equals("imageContent", StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(name);
                    }
                    else if (name.Equals("groupIds", StringComparison.OrdinalIgnoreCase))
                    {
                        groupIdsKeys.Add(name);
                    }
                    else if (name.Equals("rasterFontId", StringComparison.OrdinalIgnoreCase))
                    {
                        rasterFontKeys.Add(name);
                    }
                    else if (name.Equals("id", StringComparison.OrdinalIgnoreCase)
                             || name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(name);
                    }
                }

                foreach (string name in toRemove) obj.Remove(name);
                foreach (string key in groupIdsKeys) TranslateGroupIds(obj[key]);
                foreach (string key in rasterFontKeys) TranslateRasterFont(obj, key);

                foreach (var kv in obj) TranslateTimetable(kv.Value);
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array) TranslateTimetable(item);
            }
        }

        // ---------------------------------------------------------------- layout

        /// <summary>A layout fejléc kijelzőjének és jogosultsági csoportjainak fordítása.</summary>
        public bool TranslateLayoutHeader(JsonObject header)
        {
            if (!TranslateDisplay(header, "displayId")) return false;
            TranslateGroupIds(header["groupIds"]);
            return true;
        }

        /// <summary>
        /// Egy layout-elem összes hivatkozásának fordítása. Hamissal tér vissza, ha az elem nem
        /// vihető át - ilyenkor a hívó kihagyja.
        /// </summary>
        public bool TranslateLayoutElement(JsonObject item, string itemName)
        {
            bool valid = true;

            valid &= TranslateByLabel(item, "elementTypeId", _source.ElementTypes, _target.ElementTypes, "elemtípus", itemName);
            item.Remove("elementTypeLabel");

            valid &= TranslateByLabel(item, "anchorX", _source.AnchorX, _target.AnchorX, "AnchorX érték", itemName);
            item.Remove("anchorXLabel");

            valid &= TranslateByLabel(item, "anchorY", _source.AnchorY, _target.AnchorY, "AnchorY érték", itemName);
            item.Remove("anchorYLabel");

            valid &= TranslateByLabel(item, "fontColor", _source.TextColors, _target.TextColors, "TextColor érték", itemName);
            valid &= TranslateByLabel(item, "backgroundColor", _source.TextColors, _target.TextColors, "TextColor érték", itemName);

            valid &= TranslateByName(item, "gridId", _source.Grids, _target.Grids, "rács", itemName);
            valid &= TranslateByName(item, "dynamicTimetableId", _source.Timetables, _target.Timetables, "dinamikus menetrend", itemName);

            valid &= TranslateElementRasterFont(item, "rasterFontId", itemName);
            item.Remove("ttFontName");

            TranslateGroupIds(item["groupIds"]);
            return valid;
        }

        // ---------------------------------------------------------------- kép

        /// <summary>Egy képkeresés eredménye: mit talált a fordító a cél szerveren.</summary>
        public enum ImageLookupKind
        {
            /// <summary>Nincs mit tenni (nincs kép, vagy azonos szerver).</summary>
            NothingToDo,
            /// <summary>A cél szerveren megvan ugyanilyen nevű kép.</summary>
            FoundOnTarget,
            /// <summary>A célon nincs meg - fel kell tölteni a forrásról.</summary>
            NeedsUpload,
            /// <summary>A forrás szerveren sem található - az elem nem vihető át.</summary>
            MissingOnSource
        }

        public record ImageLookup(ImageLookupKind Kind, int SourceId, int TargetId, string Name);

        /// <summary>
        /// Megnézi, mi a helyzet egy kép hivatkozással. A tényleges feltöltést a hívó végzi,
        /// mert az hálózatot igényel; utána a <see cref="RegisterUploadedImage"/> hívandó.
        /// </summary>
        public ImageLookup LookupImage(JsonObject item, string field, string itemName)
        {
            if (IsSameServer) return new ImageLookup(ImageLookupKind.NothingToDo, 0, 0, "");
            if (item[field] is not JsonValue value || !value.TryGetValue(out int sourceId))
                return new ImageLookup(ImageLookupKind.NothingToDo, 0, 0, "");

            var sourceImage = _source.Images.FirstOrDefault(e => e.Id == sourceId);
            if (sourceImage == null)
            {
                Report.Skip($"a(z) {_source.ServerKey} szerveren nincs kép ezzel az azonosítóval: {sourceId} ({itemName}).");
                return new ImageLookup(ImageLookupKind.MissingOnSource, sourceId, 0, "");
            }

            var targetImage = _target.Images.FirstOrDefault(e => NameEquals(e.Name, sourceImage.Name));
            if (targetImage != null)
            {
                if (targetImage.Id != sourceId) Report.Converted("kép");
                item[field] = targetImage.Id;
                return new ImageLookup(ImageLookupKind.FoundOnTarget, sourceId, targetImage.Id, sourceImage.Name);
            }

            return new ImageLookup(ImageLookupKind.NeedsUpload, sourceId, 0, sourceImage.Name);
        }

        /// <summary>
        /// Egy objektum jogosultsági csoportjainak fordítása helyben. A képfeltöltéshez kell:
        /// a forrás csoportazonosítói a cél szerveren mást jelentenének.
        /// </summary>
        public void TranslateGroupIdsOf(JsonObject owner) => TranslateGroupIds(owner["groupIds"]);

        /// <summary>
        /// A feltöltött kép beírása a mezőbe és a névtáblába, hogy egy második hivatkozás már
        /// megtalálja, és ne töltsük fel kétszer.
        /// </summary>
        public void RegisterUploadedImage(JsonObject item, string field, int newId, string name)
        {
            _target.Images.Add(new NamedEntity(newId, name));
            item[field] = newId;
            Report.Converted("feltöltött kép");
        }

        // ---------------------------------------------------------------- megálló-kötés

        /// <summary>
        /// Egy megálló-kötés fordítása. Null-lal tér vissza, ha a kötés nem vihető át.
        /// </summary>
        public JsonObject? TranslateSlide(JsonObject slide, int newLayoutId)
        {
            string stopName = ResolveName(_source.Stops, slide["stopId"]);
            string stateName = ResolveName(_source.States, slide["stateId"]);

            var stop = _target.Stops.FirstOrDefault(s => NameEquals(s.Name, stopName));
            if (stop == null)
            {
                Report.Skip($"a megálló-kötés kimarad: a(z) {_target.ServerKey} szerveren nincs '{stopName}' nevű megálló.");
                return null;
            }

            var state = _target.States.FirstOrDefault(s => NameEquals(s.Name, stateName));
            if (state == null)
            {
                Report.Skip($"a megálló-kötés kimarad ({stopName}): a(z) {_target.ServerKey} szerveren nincs '{stateName}' nevű állapot.");
                return null;
            }

            return new JsonObject
            {
                ["layoutId"] = newLayoutId,
                ["stopId"] = stop.Id,
                ["stateId"] = state.Id,
                ["prioritySn"] = slide["prioritySn"]?.GetValue<int?>() ?? 1,
                ["timer"] = slide["timer"]?.GetValue<int?>() ?? 0,
                ["informationSlide"] = slide["informationSlide"]?.GetValue<bool?>() ?? false,
                ["description"] = slide["description"]?.GetValue<string?>()
            };
        }

        public string DescribeSlide(JsonObject slide)
            => $"{ResolveName(_source.Stops, slide["stopId"])} / {ResolveName(_source.States, slide["stateId"])}";

        // ---------------------------------------------------------------- belső fordítók

        /// <summary>Azonosító -> címke -> azonosító fordítás (elemtípus, anchor, szín).</summary>
        private bool TranslateByLabel(JsonObject item, string field,
            Dictionary<int, string> sourceMap, Dictionary<int, string> targetMap, string what, string itemName)
        {
            if (item[field] is not JsonValue value || !value.TryGetValue(out int sourceId))
                return true;

            if (!sourceMap.TryGetValue(sourceId, out string? label) || string.IsNullOrEmpty(label))
            {
                Report.Skip($"a(z) {_source.ServerKey} szerveren nincs {field}={sourceId} ({itemName}).");
                return false;
            }

            // A találat hiányát a null érték jelzi, nem a 0-s kulcs: a 0 elvileg érvényes azonosító is lehet.
            var match = targetMap.FirstOrDefault(kvp => NameEquals(kvp.Value, label));
            if (match.Value == null)
            {
                Report.Skip($"a(z) {_target.ServerKey} szerveren nincs '{label}' {what} ({itemName}).");
                return false;
            }

            if (match.Key != sourceId) Report.Converted(what);
            item[field] = match.Key;
            return true;
        }

        /// <summary>Azonosító -> név -> azonosító fordítás (rács, menetrend).</summary>
        private bool TranslateByName(JsonObject item, string field,
            List<NamedEntity> sourceList, List<NamedEntity> targetList, string what, string itemName)
        {
            if (IsSameServer) return true;
            if (item[field] is not JsonValue value || !value.TryGetValue(out int sourceId))
                return true;

            var source = sourceList.FirstOrDefault(e => e.Id == sourceId);
            if (source == null)
            {
                Report.Skip($"a(z) {_source.ServerKey} szerveren nincs {what} ezzel az azonosítóval: {sourceId} ({itemName}).");
                return false;
            }

            var target = targetList.FirstOrDefault(e => NameEquals(e.Name, source.Name));
            if (target == null)
            {
                Report.Skip($"a(z) {_target.ServerKey} szerveren nincs '{source.Name}' nevű {what} ({itemName}).");
                return false;
            }

            if (target.Id != sourceId) Report.Converted(what);
            item[field] = target.Id;
            return true;
        }

        /// <summary>A layout-elem raszterfontja: hiánya csak ezt az elemet ejti ki.</summary>
        private bool TranslateElementRasterFont(JsonObject item, string field, string itemName)
        {
            if (IsSameServer) return true;
            if (item[field] is not JsonValue value || !value.TryGetValue(out int sourceId))
                return true;

            var target = FindMatchingFont(sourceId, itemName, out string? description);
            if (target == null)
            {
                Report.Skip(description!);
                return false;
            }

            if (target.Id != sourceId) Report.Converted("raszterfont");
            item[field] = target.Id;
            return true;
        }

        /// <summary>
        /// A menetrend cellájának raszterfontja: itt nincs "hagyjuk ki ezt az egy elemet", mert a
        /// cella font nélkül nem menthető - a hiány az egész műveletet blokkolja.
        /// </summary>
        private void TranslateRasterFont(JsonObject parent, string field)
        {
            if (IsSameServer) return;
            if (parent[field] is not JsonValue value || !value.TryGetValue(out int sourceId)) return;

            var target = FindMatchingFont(sourceId, "menetrend cella", out string? description);
            if (target == null)
            {
                Report.Block(description!);
                return;
            }

            if (target.Id != sourceId) Report.Converted("raszterfont");
            parent[field] = target.Id;
        }

        /// <summary>A raszterfontot a családnév és a méret párosa azonosítja.</summary>
        private RasterFontInfo? FindMatchingFont(int sourceId, string itemName, out string? description)
        {
            var source = _source.RasterFonts.FirstOrDefault(f => f.Id == sourceId);
            if (source == null)
            {
                description = $"a(z) {_source.ServerKey} szerveren nincs raszterfont ezzel az azonosítóval: {sourceId} ({itemName}).";
                return null;
            }

            var target = _target.RasterFonts.FirstOrDefault(f => NameEquals(f.TtFontName, source.TtFontName) && f.Size == source.Size);
            if (target == null)
            {
                description = $"'{source.TtFontName}' {source.Size}px raszterfont hiányzik a(z) {_target.ServerKey} szerverről";
                return null;
            }

            description = null;
            return target;
        }

        private bool TranslateDisplay(JsonObject parent, string field)
        {
            if (parent[field] is not JsonValue value || !value.TryGetValue(out int sourceId))
                return true;

            var source = _source.Displays.FirstOrDefault(d => d.Id == sourceId);
            if (source == null)
            {
                Report.Block($"a(z) {_source.ServerKey} szerveren nincs kijelző ezzel az azonosítóval: {sourceId}");
                return false;
            }

            var target = _target.Displays.FirstOrDefault(d => NameEquals(d.Name, source.Name));
            if (target == null)
            {
                Report.Block($"'{source.Name}' kijelző hiányzik a(z) {_target.ServerKey} szerverről");
                return false;
            }

            if (target.Id != sourceId) Report.Converted("kijelző");
            parent[field] = target.Id;
            return true;
        }

        /// <summary>
        /// A jogosultsági csoportok fordítása helyben. A nem párosítható csoport kimarad, de a
        /// többi átmegy - egy hiányzó csoport miatt nem érdemes az egész elemet elejteni.
        /// </summary>
        private void TranslateGroupIds(JsonNode? node)
        {
            if (IsSameServer) return;
            if (node is not JsonArray array || array.Count == 0) return;

            var translated = new List<int>();
            foreach (var item in array)
            {
                if (item?.GetValue<int?>() is not int sourceId)
                {
                    Report.Skip($"érvénytelen jogosultsági csoport (groupId): {item}");
                    continue;
                }

                var source = _source.Groups.FirstOrDefault(g => g.Id == sourceId);
                if (source == null)
                {
                    Report.Skip($"a(z) {_source.ServerKey} szerveren nincs groupId={sourceId} jogosultsági csoport.");
                    continue;
                }

                var target = _target.Groups.FirstOrDefault(g => NameEquals(g.Name, source.Name));
                if (target == null)
                {
                    Report.Skip($"a(z) {_target.ServerKey} szerveren nincs '{source.Name}' jogosultsági csoport.");
                    continue;
                }

                if (target.Id != sourceId) Report.Converted("jogosultsági csoport");
                translated.Add(target.Id);
            }

            array.Clear();
            foreach (int id in translated) array.Add(id);
        }

        private static string ResolveName(List<NamedEntity> table, JsonNode? idNode)
        {
            if (idNode?.GetValue<int?>() is not int id) return "?";
            return table.FirstOrDefault(e => e.Id == id)?.Name ?? "?";
        }
    }
}

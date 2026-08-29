## Quick context

- Windows Forms segédeszköz (net10.0-windows), ami Smartpage **dinamikus menetrendeket** és
  **layoutokat** másol: szerveren belül (duplikálás) és szerverek között, oda-vissza.
- A backend HTTP API-ját hívja. A szerveren **nincs** `copy` vagy `duplicate` végpont —
  a másolást ez az eszköz építi fel lekérdezésekből és mentésekből.
- Az API-hoz nincs dokumentáció (a Swagger 500-at ad). A felderített referencia:
  **[`docs/smartpage-api.md`](../docs/smartpage-api.md)** — végponttérkép, hibaformátum,
  kikényszerített üzleti szabályok, szerverek közötti eltérések. Olvasd el, mielőtt
  API-hívást írsz vagy módosítasz.

## Big picture

| Fájl | Szerep |
|---|---|
| `MainForm.cs` | UI + HTTP + JSON-transzformáció + üzleti logika (1153 sor, mindent visz) |
| `MainForm.Designer.cs` | kézzel írt UI-felépítés, nem a designer generálta |
| `LoginDialog.cs` | bejelentkezés az auth-server-backendhez, token + session megszerzése |
| `SmartpageApiClient.cs` | UI-mentes wrapper a listalekérdezésekhez (a Load/Save hívások még nem itt vannak) |
| `Models/TimetableItem.cs` | menetrend: `DynamicRow` / `DynamicCell` |
| `Models/LayoutItem.cs` | layout fejléc (brief) |
| `Models/LayoutItems.cs` | layout-elem + beágyazott `Announcement` |
| `Models/ElementType.cs` | elemtípus |

**Szerverkulcsok** (`_baseUrls` a `MainForm.cs`-ben, `_authUrls` a `LoginDialog.cs`-ben):
`DEV`, `DEMO`, `PROD`, `PROD2`. Új szerver felvételéhez **mindkét** szótárat bővíteni kell,
plusz a két ComboBox inicializálóját a `MainForm` konstruktorában.

## Adatfolyam

**Menetrend beolvasása:** `GET dynamic-timetable/load?id={id}` → `TimetableItem`.
**Menetrend mentése:** a modellt JSON-fává szerializálja, `RemoveIdProperties` végigjárja
(ID-ket töröl vagy fordít), majd `POST dynamic-timetable/save`.

**Layout beolvasása:** `GET layout/load/{id}` (fejléc) + `GET element/list/layoutId?layoutId={id}` (elemek).
**Layout mentése:** `POST layout/save` → visszakapott új ID → `POST element/save/all`.

> Az `element/save/all` **teljes cserét** végez: törli a layout összes elemét, és újakat
> hoz létre. Nem hozzáfűz.

## A központi elv: ID-k helyett nevek

A szerverek között az ID-k nem hordozhatók, a **nevek** viszont stabilak. Minden
hivatkozást név szerint kell újrakeresni a cél szerveren:

| Mező | Párosítás alapja |
|---|---|
| `displayId` | kijelző neve |
| `groupIds` | csoportnév |
| `rasterFontId` | `ttFontName` + `size` |
| `elementTypeId` | `typeLabel` |
| `anchorX`, `anchorY`, `fontColor`, `backgroundColor` | enum `label` |
| `imageId`, `gridId`, `dynamicTimetableId` | név |

**Névösszehasonlításnál mindig `Trim()`** — a PROD-on van olyan fontcsalád, aminek a neve
záró szóközzel szerepel (`"SourceSans3-Bold "`), a DEMO-n anélkül.

**Azonos szerveren belül nem szabad fordítani** — ott az eredeti ID a helyes. A
megkülönböztetés a `cmbServerLoad` és `cmbServerSave` kiválasztott értékének
összehasonlításával történik (lásd `ConvertGroupIds`, `RemoveIdProperties`).

## Konvenciók

- **Szerializálás:** beolvasáskor `PropertyNameCaseInsensitive = true`; mentéskor
  `JsonNamingPolicy.CamelCase` + `DefaultIgnoreCondition = WhenWritingNull`.
- **JSON-manipuláció:** `System.Text.Json.Nodes` (`JsonNode` / `JsonObject` / `JsonArray`).
  Számértéket `JsonValue.ReplaceWith(...)`-tal kell cserélni — a `parentObj[key] = ...`
  minta csak akkor működik, ha tényleg objektum van a kezünkben.
- **Státuszüzenet:** `SetStatus(string, Color)` — zöld = siker, narancs = figyelmeztetés,
  piros = hiba. Magyar nyelvű, emoji-előtaggal.
- **Eseménykezelők:** `async void` (WinForms konvenció).
- **TLS:** a tanúsítvány-ellenőrzés csak a `smartpage-dev.hclinear.hu` hosztra van
  megkerülve (`CertBypassHost`). Ne tágítsd globálisra.
- **HttpClient:** a Load és a Save **külön példány**, közös handlerrel. Ne vond össze őket:
  az `ApplyAuthHeaders` törli a fejléceket, így egy közös példány a másik oldal
  hitelesítését is elrontaná.

## Hibakezelés

A backend strukturált, magyar hibákat ad (`fieldErrors` / `logicalErrors`, 422). Ezeket
**bontsd ki**, ne nyers JSON-ként írd a státuszmezőbe. A formátumot és a visszatérő
üzeneteket a [`docs/smartpage-api.md`](../docs/smartpage-api.md) 3. fejezete írja le.

Mentés előtt érdemes ellenőrizni a szerver üzleti szabályait (első elem típusa, háttérkép
felbontása, névegyediség) — lásd ugyanott a 4. fejezetet.

## Ismert hiányosságok

- A raszterfontok **nem vihetők át** szerverek között: a tartalmuk nem olvasható ki az
  API-ból. Csak jelezni lehet, ha hiányoznak a célról.
- A másolás **nem hozza létre a slide-okat** (megálló ↔ layout kapcsolat), így a másolat
  egyetlen megállón sem jelenik meg. Szándék szerint: szerverek között követnie kellene,
  szerveren belüli duplikálásnál nem.
- Nincs automata teszt.

## Build / futtatás

```powershell
dotnet build "DuplicateTT.csproj"
```

Önálló exe (a `.vscode/tasks.json`-ban is szerepel):

```powershell
dotnet publish "DuplicateTT.csproj" -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Ha módosítasz

- **Új mező a backend válaszában** → vedd fel a `Models/` megfelelő osztályába, különben a
  másoláson **csendben elveszik** (így veszett el eddig a `DynamicCell.delayThreshold`).
- **Új végpont** → előbb nézd meg a `docs/smartpage-api.md` térképét; ha nincs benne,
  `OPTIONS`-szel deríthető fel (a módszer a fájl 7. fejezetében).
- **Író hívás tesztelése** → soha ne a PROD-on. Homokozó szerverre van szükség.

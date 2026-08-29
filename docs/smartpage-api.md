# Smartpage backend API — felderített referencia

A backendhez nincs publikus dokumentáció: a `backend/v3/api-docs` és a Swagger felület
egyaránt 500-as hibát ad. Ez a leírás **mérésből** készült, a PROD2 homokozó szerveren
(2026-08-29), olvasó és író hívásokkal, valamint a PROD és DEMO szerverek olvasó
összevetéséből.

> **Fontos:** ez megfigyelt viselkedés, nem szerződés. Ha a backend változik, ez a fájl
> elavul. A felderítés módszere a végén szerepel — megismételhető.

---

## 1. Hitelesítés

Két lépés, cookie-alapú munkamenettel:

```
POST {host}/auth-server-backend/api/v1/auth/sign-in
     { "username": "...", "password": "..." }
     → 200, Set-Cookie: session-id=..., authentication-code=...

POST {host}/auth-server-backend/api/v1/auth/token
     (cookie-kkal, üres body)
     → 200, { "accessToken": "..." }
```

Minden további hívás fejlécei:

```
Authorization: Bearer {accessToken}
sessionid: {session-id cookie értéke}
```

A `sessionid` fejléc nélkül a válasz `401 {"type":"TOKEN_MISSING"}` — függetlenül attól,
hogy az útvonal létezik-e. A token lejáratát a kliensnek kezelnie kell; automatikus
frissítés nincs.

**Base URL:** `{host}/backend/api/v1`

| Kulcs | Host |
|---|---|
| DEV | `https://smartpage-dev.hclinear.hu` |
| DEMO | `https://smartpage-demo.hclinear.hu` |
| PROD | `https://smartpage.hclinear.hu` |
| PROD2 | `https://smartpage2.hclinear.hu` |

---

## 2. Végponttérkép

Az alapminta entitásonként: `list` (lekérdezés), `load` (egy elem), `save` (létrehozás
**és** frissítés), `remove` (törlés). Az eltérések alább jelölve.

### Layout

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| GET | `layout/list` | fejlécek, elemek nélkül |
| GET | `layout/load/{id}` | path-paraméter, nem query |
| POST | `layout/save` | `id` nélkül **létrehoz**, `id`-vel **frissít**; a válasz törzse az ID |
| DELETE | `layout/remove?id={id}` | **kaszkádol** az elemekre és a slide-okra |

Kötelező mezők mentéskor: `name`, `displayId`.

### Element (layout-elem)

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| GET | `element/list/layoutId?layoutId={id}` | egy layout összes eleme |
| GET | `element/load/{id}` | |
| POST | `element/save` | egyetlen elem |
| POST | `element/save/all` | `{ layoutId, elements[] }` — **teljes csere!** |
| DELETE | `element/remove?id={id}` | |

> **`element/save/all` figyelmeztetés:** a hívás törli a layout *összes* meglévő elemét,
> és újakat hoz létre új ID-kkel. Nem hozzáfűz. Meglévő layoutra hívva adatvesztés.

Kötelező mezők egyetlen elem mentésekor: `layoutId`, `name`, `prioritySn`,
`elementTypeId`, `x`, `y`, `width`, `height`. A `save/all` csak a `layoutId`-t követeli
meg a burkoló objektumon, az elemeket elemenként validálja (`elements[N].mezőnév`).

### Dynamic timetable

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| GET | `dynamic-timetable/list` | tartalmazza a `relatedLayouts` mezőt |
| GET | `dynamic-timetable/load?id={id}` | **query**-paraméter, nem path |
| POST | `dynamic-timetable/save` | sorok és cellák együtt, egy hívásban |
| DELETE | `dynamic-timetable/remove?id={id}` | |

Kötelező mezők: `width`, `height`. A `name` **nem** kötelező (de az egyediség igen, ha meg van adva).

### Image

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| GET | `image/list` | **minden** kép base64 tartalommal — PROD2-n 5,24 MB |
| POST | `image/load` | `{ "id": N }` — POST, nem GET! |
| POST | `image/save` | `file` mezőben base64; szerverek közötti átvitelre alkalmas |
| DELETE | `image/remove?id={id}` | |

Kötelező mezők: `name`, `status`, `type`, és új rekordnál nem üres `file`.
Mérés: egy `image/load`-dal letöltött kép `image/save`-vel új néven visszatöltve
**byte-azonos** másolatot ad.

### Announcement (közlemény)

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| POST | `announcement/list` | **POST**, üres body; GET-tel 500-at ad |
| POST | `announcement/load` | `{ "id": N }` |
| POST | `announcement/save` | |
| DELETE | `announcement/remove?id={id}` | |

A közlemény elemei az **`items`** tömbben érkeznek (nem `textAnnouncements` /
`imageAnnouncements`). A layout-elembe beágyazva is megjelenik; ha `id` nélkül küldjük,
új közlemény jön létre.

### Slide — megálló ↔ layout kapcsolat

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| GET | `slide/list` | **nincs szűrt változata**; a teljes lista lassú (percek) |
| GET | `slide/load/{id}` | |
| POST | `slide/save` | |
| DELETE | `slide/remove?id={id}` | |

Kötelező mezők: `stopId`, `layoutId`, `stateId`, `prioritySn` (**1 és 15 között**).

> Ez köti a layoutot a megállóhoz. A `layout/list` `stopNamesConcatenated` mezője ebből
> **származtatott, csak olvasható** adat — átmásolni nem lehet, a kapcsolatot a slide tartja.
> A PROD2-n 357 layoutból 294-nek van megálló-kötése.

### Raster font

| Metódus | Útvonal | Megjegyzés |
|---|---|---|
| GET | `raster-font/list` | lapos lista |
| GET | `raster-font/listFonts` | családonként csoportosítva, `rasterFonts[]` tömbbel |
| GET | `raster-font/load/{id}` | **nem adja vissza a fájl tartalmát** |
| POST | `raster-font/save` | új rekordhoz fájl kötelező |
| DELETE | `raster-font/remove?id={id}` | |

> **Korlát:** a font tartalma egyetlen végponton sem olvasható ki, a `save` viszont
> követeli. Ezért **a raszterfontok API-ból nem vihetők át** szerverek között. A
> `listFonts` `content` mezője a család szintjén egy előnézeti kép, nem a fontfájl.

### Törzsadatok (csak olvasás a másoláshoz)

| Útvonal | Metódus | Tartalom |
|---|---|---|
| `group/list` | GET | jogosultsági csoportok |
| `display/list`, `display/load/{id}` | GET | kijelzőtípusok |
| `element-type/list` | GET | elemtípusok, `typeLabel` mezővel |
| `grid/list`, `grid/load/{id}` | GET | menetrendi rácsok |
| `stop/list`, `stop/load/{id}` | GET | megállók |
| `stop-template/list` | GET | megállósablonok |
| `user/list`, `user/load/{id}` | GET | felhasználók, `roles` mezővel |
| `enum/list/enum/values/{Enum}` | GET | `AnchorX`, `AnchorY`, `TextColor` |

Ezeknek is van `save` / `remove` párja (`group/save`, `display/save`, `grid/save`,
`grid/save/all`, `stop/save`, `element-type/save`, `enum/update/all` …), de a másoláshoz
nem kellenek.

**Nincs `copy` és nincs `duplicate` végpont egyetlen entitáson sem** — ezért létezik ez az eszköz.

---

## 3. Hibakezelés

A backend strukturált, **magyar nyelvű** hibákat ad. Ezeket ki kell bontani, nem nyersen
a felhasználó elé tenni.

```json
{
  "fieldErrors":   [ { "fieldName": "elements[1].imageId",
                       "errorMsg": "A megadott érték nem szerepel a nyilvántartásban!" } ],
  "logicalErrors": [ { "errorMsg": "Az első elemenek képnek vagy közleményhelynek kell lennie!" } ],
  "statusCode": 422
}
```

| Kód | Jelentés |
|---|---|
| 422 | validációs hiba — `fieldErrors` és/vagy `logicalErrors` kitöltve |
| 404 | az erőforrás nem létezik (`"Layout does not exist with id: N"`) |
| 401 | `{"type":"TOKEN_MISSING"}` — hiányzó vagy lejárt hitelesítés |
| 500 | `"No static resource api/v1/..."` = nincs ilyen útvonal<br>`"Request method 'GET' is not supported"` = van útvonal, rossz metódus |

Visszatérő `errorMsg` szövegek:

- `"A mező nem lehet üres!"` — hiányzó kötelező mező
- `"A megadott érték nem szerepel a nyilvántartásban!"` — nem létező hivatkozott ID
- `"A megadott értékek valamelyike nem szerepel a nyilvántartásban!"` — tömbnél (pl. `groupIds`)
- `"A megadott érték már szerepel a nyilvántartásban!"` — névütközés

---

## 4. Kikényszerített üzleti szabályok

Egyik sincs dokumentálva; mind mérésből derült ki. A kliensnek **mentés előtt** érdemes
ellenőriznie őket.

| Szabály | Hol csap le |
|---|---|
| Az első elem (`prioritySn` = 1) képnek vagy közleményhelynek kell lennie | `element/save/all` |
| A háttérképet tartalmazó elem felbontása egyezzen a kijelzőével (1200×1600) | `element/save/all` |
| Minden hivatkozott ID létezzen (`imageId`, `rasterFontId`, `displayId`, `groupIds` …) | minden `save` |
| A név egyedi legyen — layout, menetrend, közlemény | minden `save` |
| Új kép és font létrehozásához nem üres fájl kötelező | `image/save`, `raster-font/save` |
| A slide prioritása 1 és 15 közötti | `slide/save` |

---

## 5. Amit a modellek nem ismernek

A JSON-válaszok mezőit végigmérve (mind a 27 menetrend összes cellája, 90 layout összes
eleme) két eltérés van a `Models/` osztályokhoz képest:

| Mező | Hol | Állapot |
|---|---|---|
| `delayThreshold` | `DynamicCell` | **hiányzik a modellből** → másoláskor elveszik |
| `items` | `Announcement` | a modell `textAnnouncements` / `imageAnnouncements` mezőt vár, ilyet a szerver nem küld |

Mivel az API-nak nincs dokumentációja, érdemes a beolvasott nyers JSON-t összevetni a
modellel, és ismeretlen mezőnél naplózni — különben a következő ilyen eltérés is
észrevétlen marad.

---

## 6. Szerverek közötti eltérések

Az eszköz feltevése — *a nevek stabilak, az ID-k nem* — mérve (2026-08-29):

**Strukturális táblák: teljes egyezés.** Elemtípus (10), rács (9), TextColor (16),
AnchorX/AnchorY (3+3), kijelző (1). Csoportnál egyetlen eltérés: a „V-Busz" hiányzik a DEMO-ról.

**Tartalmi táblák: jelentős eltérés.**

| Irány | Használt fontból átvihető | Képnév megvan a célon |
|---|---|---|
| PROD → PROD2 | 27 / 34 | 397 / 457 (87%) |
| PROD → DEMO | 33 / 34 | 74 / 457 (16%) |
| PROD2 → PROD | — | 397 / 444 (89%) |

> **Névcsapda:** a PROD-on a font családneve `"SourceSans3-Bold "` — **záró szóközzel**,
> a DEMO-n anélkül. Minden név szerinti párosításnál `Trim()` kell; enélkül a PROD → DEMO
> irányban 7 használt font párosítása bukik el egy helyett.

---

## 7. A felderítés módszere

Két nem destruktív trükk, amivel az egész térkép elkészült — megismételhető, ha a backend
változik:

**1. OPTIONS + `Allow` fejléc.** Létező útvonalra 200-at ad, és felsorolja a metódusokat:

```
OPTIONS layout/save   → 200, Allow: POST,OPTIONS
OPTIONS group/list    → 200, Allow: GET,HEAD,OPTIONS
```

**2. A hibaüzenet megkülönbözteti a nem létező útvonalat a rossz metódustól:**

```
GET nem/letezik   → 500 "No static resource api/v1/nem/letezik."
GET layout/save   → 500 "Request method 'GET' is not supported"
```

**3. Üres POST a `save` végpontokra** → 422 a kötelező mezők pontos listájával.

Így 457 útvonalat lehetett végigpróbálni egyetlen írás nélkül.

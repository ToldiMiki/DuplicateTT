# Jelentések

Két önálló HTML-oldal a 2026-08-29-i átvilágításról. Böngészőben megnyithatók, semmilyen
külső függőségük nincs a Google Fonts betűtípusokon kívül (ezek nélkül is olvashatók).

| Fájl | Mit rögzít |
|---|---|
| [`atvilagitas-terv.html`](atvilagitas-terv.html) | **A munka megkezdése előtti állapot.** 27 lelet, a backend API mérésből felderített térképe, hatfázisú ütemterv becslésekkel, és a nyitott kérdések. A `main` ág `24f369f` commitja alapján. |
| [`atvilagitas-eredmeny.html`](atvilagitas-eredmeny.html) | **A munka elvégzése után.** Fázisonkénti eredmény commit-hivatkozásokkal, a tesztek mutációs bizonyítása, a 27 lelet állástáblája, és amit a mérés az első elemzésből felülírt. |

A terv-dokumentumot érdemes megtartani: a becslések és a leletek részletes leírásai
összevethetők azzal, ami végül elkészült.

## Miért nem a kódban van mindez?

A két oldal a *döntéseket* és a *méréseket* rögzíti, nem a kódot. Az API-ról szóló
gyakorlati tudás — végponttérkép, hibaformátum, üzleti szabályok — a
[`docs/smartpage-api.md`](../smartpage-api.md) fájlban van, mert az a napi munkához kell.

Ezek az oldalak egy pillanatképet őriznek arról, hogy mi volt a helyzet, és miért döntöttünk
úgy, ahogy. A PROD2 homokozó, amin minden mérés készült, azóta megszűnt — ez a két fájl
(a `smartpage-api.md`-vel együtt) az egyetlen fennmaradt nyoma.

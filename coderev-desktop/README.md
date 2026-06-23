# coderev Desktop (.NET / Avalonia)

Cross-platform (Windows, Linux, macOS) **asztali GUI** a `coderev` PR-review
eszközhöz. A grafikus felület átláthatóbbá teszi az áttekintést: a lépések, a
diff, a haladás és az AI válasza vizuálisan, strukturáltan jelenik meg.

A GUI **nem írja újra** a Go-logikát: a meglévő `coderev` binárist hajtja meg
mint motort, és egy strukturált esemény-protokollon (NDJSON) keresztül
kommunikál vele. Így a két projekt egy forrásból, közös magból dolgozik.

## Állapot

A váz (D0–D2) **implementálva és buildel**:

- **D0 – motor-szerződés:** a Go `coderev` bináris `--format json` kapcsolóval
  NDJSON eseményeket ad (a fő projektben, `internal/ui/jsonreporter.go`).
- **D1 – `CodeRev.Core`:** process-indítás, NDJSON-parser, `ReviewSession` fold,
  bináris-felderítés. **15 xUnit teszt zöld.**
- **D2 – `CodeRev.App`:** Avalonia MVVM GUI (setup-sáv, élő lépés-lista, diff- és
  review-fülek, Stop). Buildel.
- **D3 – Gazdag nézetek:** színezett, sorszámozott diff fájl-navigációval;
  szekciókra bontott, súlyozott review-kártyák (hu/en).
- **D4 – Config + agent:** grafikus `.coderev.json` szerkesztő (a CLI `init`
  megfelelője) beépített + egyedi agent kezeléssel.
- **D5 – Csiszolás:** review-előzmények (auto-mentés + visszatöltés), export
  Markdown/HTML-be, világos/sötét téma, self-contained csomagolás. **40 teszt.**

## Csomagolás (self-contained, Linuxra is)

```powershell
./publish.ps1 -Rid win-x64,linux-x64   # a coderev motort is mellécsomagolja
```

Vagy kézzel: `dotnet publish src/CodeRev.App -c Release -r linux-x64 --self-contained true`.

## Build és futtatás

```bash
# A motor binárist előbb építsd meg a fő projektben:
cd ..  &&  go build -o coderev.exe ./cmd/coderev   # vagy coderev (Linux/macOS)

cd coderev-desktop
dotnet build                                       # teljes solution
dotnet test  src/CodeRev.Core.Tests                # a mag tesztjei
dotnet run --project src/CodeRev.App               # a GUI indítása
```

A GUI a motort így keresi: `CODEREV_BIN` env-változó → az app mellé csomagolt
bináris → `PATH`. Egyszerű fejlesztéshez állítsd be:
`CODEREV_BIN=/út/coderev(.exe)`.

## Dokumentumok

- **[docs/PLAN.md](docs/PLAN.md)** – teljes terv: technológiai döntés,
  integrációs architektúra, esemény-protokoll, projektstruktúra, MVVM nézetek,
  GUI-specifikus továbbfejlesztési lehetőségek, ütemterv, nyitott kérdések.

## Struktúra

```
coderev-desktop/
├── CodeRev.Desktop.slnx
├── src/
│   ├── CodeRev.Core/        Protocol (esemény-modell, NDJSON-parser),
│   │                        Engine (runner, bináris-felderítés), Models (fold)
│   ├── CodeRev.Core.Tests/  xUnit (15 teszt)
│   └── CodeRev.App/         Avalonia GUI (MVVM): Views + ViewModels
└── docs/PLAN.md
```

> Megjegyzés: a NuGet `Tmds.DBus.Protocol` figyelmeztetés egy tranzitív
> Avalonia-Linux függőség; nem a mi kódunk. Avalonia-frissítéskor megszűnik.

## Dióhéjban

- **Keretrendszer:** Avalonia UI 11 + .NET 8 (MVVM). Valódi Linux desktop
  támogatás (a MAUI ezt nem adja).
- **Motor:** a `coderev` Go bináris, `--format json` esemény-kimenettel.
- **Integráció:** a GUI elindítja a folyamatot, olvassa az NDJSON eseményeket,
  és élőben rendereli (lépések, diff, streamelt válasz, összegzés).

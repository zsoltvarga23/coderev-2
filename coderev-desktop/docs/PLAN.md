# coderev Desktop – Terv (.NET / Avalonia, cross-platform GUI)

> Cross-platform asztali GUI a `coderev` PR-review eszközhöz. A cél egy
> **átláthatóbb áttekintés**: a review folyamata, a diff és az AI válasza
> grafikusan, strukturáltan jelenik meg – Windowson, **Linuxon** és macOS-en is.

---

## 1. Célok

1. **Cross-platform desktop**, valódi Linux-támogatással.
2. **Átláthatóbb review** a konzolnál: diff-nézet, szekciókra bontott eredmény,
   élő haladás, navigálható találatok.
3. **A Go-motor újrahasználata** – nincs duplikált logika; a GUI a meglévő
   `coderev` binárist hajtja meg.
4. **Konzisztens élmény** a CLI-vel (ugyanazok a beállítások, ugyanaz a prompt és
   review).

## 2. Nem-célok (első kör)

- Nem cél saját AI-integráció a GUI-ban (az ügynököt továbbra is a Go-motor hívja).
- Nem cél mobil (iOS/Android) – ez asztali alkalmazás.
- Nem cél a Go-logika C#-ra portolása.

---

## 3. Technológiai döntés

| Szempont | Választás | Indoklás |
|---|---|---|
| UI keretrendszer | **Avalonia UI 11** | Az egyetlen érett .NET UI stack **valódi Linux desktop** támogatással. XAML + MVVM, a WPF-hez közeli élmény. |
| Runtime | **.NET 8 (LTS)** | Cross-platform, hosszú támogatás, single-file/AOT publikálás. |
| Architektúra | **MVVM** (CommunityToolkit.Mvvm) | Tesztelhető ViewModelek, adatkötés. |
| Markdown render | **Markdown.Avalonia** | Az AI review (markdown) megjelenítése natív vezérlőkben. |
| Diff-nézet | **AvaloniaEdit** (TextMate kiemelés) + saját diff-renderelő | Szintaxis-kiemelt, akár soronkénti diff. |
| Tesztelés | xUnit + a `CodeRev.Core` izolált tesztelése | A magot (process + protokoll) headless teszteljük. |

### Miért nem MAUI?
A .NET MAUI **nem támogat Linux desktopot** (csak Windows/macOS/mobil). Mivel a
Linux kifejezett követelmény, az Avalonia a helyes választás. (Alternatíva lenne
az Uno Platform; az Avalonia érettebb a desktop-fókuszhoz.)

---

## 4. Integrációs architektúra – a Go-motor mint backend

A GUI **alfolyamatként** indítja a `coderev` binárist, és egy strukturált
esemény-folyamot olvas a kimenetéről. Ez a meglévő `ui.Reporter` interfész
természetes kiterjesztése: a Go-oldalon egy új **`JSONReporter`** kell, amelyet a
`--format json` kapcsoló választ ki.

```
┌──────────────────────────────┐        spawn (stdin/args)        ┌──────────────────┐
│  CodeRev.App (Avalonia GUI)  │ ───────────────────────────────▶ │  coderev (Go)    │
│  - ViewModels                │                                   │  --format json   │
│  - Views (diff, review, log) │ ◀───── NDJSON események ───────── │  JSONReporter    │
└──────────────────────────────┘        (stdout, soronként)        └──────────────────┘
                                                                          │
                                                                   git + AI-ügynök
```

**Előnyök:**
- Egy közös mag (a Go-engine) hajtja a CLI-t és a GUI-t is → nincs logikai drift.
- A GUI nem „kaparja le" a konzolszöveget, hanem **tipizált eseményeket** kap.
- A Go-oldali változás minimális és visszafelé kompatibilis (új reporter, a
  meglévők érintetlenek).

### 4.1 Szükséges Go-oldali bővítés (a szerződés)

- Új `--format json|text` flag (default `text`). `json` esetén a `ui.New(...)`
  egy `JSONReporter`-t ad vissza, ami a `Reporter` metódusait NDJSON-sorokká
  alakítja a stdoutra.
- A diff és a mennyiségi adatok (`changed_files`, `hunks`, `diff_bytes`,
  `prompt_bytes`) egy `meta` eseményként is kimennek, hogy a GUI a diff-nézetet
  fel tudja építeni.
- Opcionális `--emit-diff` / `--emit-prompt`: a teljes diff és/vagy prompt is
  bekerül egy eseménybe (a GUI diff-nézetéhez), méretkorláttal.
- A streamelt agent-válasz `stream` eseményekként, a végén egy `review`
  eseményben a teljes markdownnal.

Ez a bővítés a meglévő Go-projektben történik; a `coderev-desktop` csak fogyasztja.

---

## 5. Esemény-protokoll (NDJSON)

Soronként egy JSON objektum, mindegyikben `type` és `ts` (epoch ms). A GUI egy
állapotgépként dolgozza fel őket.

| `type` | Mezők | Jelentés |
|---|---|---|
| `run_start` | `version`, `branch`, `base`, `lang` | A futás kezdete. |
| `step_start` | `id`, `label` | Új lépés indul. |
| `step_info` | `id`, `detail` | Mennyiségi adat a lépéshez. |
| `step_done` | `id`, `duration_ms` | Lépés kész (✓). |
| `step_fail` | `id`, `error` | Lépés hibára futott (✗). |
| `warn` | `message` | Figyelmeztetés. |
| `meta` | `changed_files[]`, `hunks`, `diff_bytes`, `prompt_bytes` | Áttekintő adatok. |
| `diff` | `unified` (string) | A teljes (esetleg csonkolt) diff a nézethez. |
| `stream` | `chunk` (string) | Az AI válasz egy darabja (élő). |
| `review` | `markdown`, `sections?` | A teljes review; opcionálisan szekciókra bontva. |
| `summary` | `total_ms`, `out_path` | Záró összegzés. |
| `done` | `exit_code` | A futás vége. |

**Példa (rövidített):**

```jsonl
{"type":"run_start","ts":1,"version":"2.0.0","branch":"feature/login","base":"origin/main","lang":"hu"}
{"type":"step_start","ts":2,"id":"diff","label":"Diff számítása"}
{"type":"step_info","ts":3,"id":"diff","detail":"12 fájl · 47 hunk · 38 KB"}
{"type":"meta","ts":3,"changed_files":["a.go","b.go"],"hunks":47,"diff_bytes":38912,"prompt_bytes":9216}
{"type":"step_done","ts":4,"id":"diff","duration_ms":210}
{"type":"step_start","ts":5,"id":"agent","label":"AI review (codex)"}
{"type":"stream","ts":6,"chunk":"## Összegzés\nA bejelentkezés..."}
{"type":"review","ts":9,"markdown":"## Összegzés\n..."}
{"type":"summary","ts":10,"total_ms":71000,"out_path":"review.md"}
{"type":"done","ts":10,"exit_code":0}
```

A `sections` (ha a Go vagy a .NET-oldali parser kitölti) a markdownból kiemelt
részek: `summary`, `major[]`, `minor[]`, `tests`, `suggestions[]`, mindegyik
elemnél opcionális `file` és `line` a navigációhoz.

---

## 6. .NET projektstruktúra

```
coderev-desktop/
├── CodeRev.Desktop.sln
├── src/
│   ├── CodeRev.Core/                 # motor-integráció, protokoll, modellek (UI nélkül)
│   │   ├── Engine/
│   │   │   ├── ICoderevRunner.cs
│   │   │   ├── CoderevRunner.cs       # process spawn, stdout olvasás, cancel
│   │   │   └── BinaryLocator.cs       # a coderev bináris megtalálása (PATH, mellékelt)
│   │   ├── Protocol/
│   │   │   ├── EventParser.cs         # NDJSON → tipizált események
│   │   │   └── Events.cs              # rekord típusok az eseményekre
│   │   ├── Models/
│   │   │   ├── ReviewSession.cs
│   │   │   ├── DiffModel.cs
│   │   │   └── ReviewResult.cs
│   │   └── Config/
│   │       └── CoderevConfig.cs       # a .coderev.json olvasása/írása (init-tükör)
│   ├── CodeRev.App/                   # Avalonia GUI (MVVM)
│   │   ├── App.axaml(.cs)
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── RunSetupViewModel.cs   # branch/base/agent választó
│   │   │   ├── ProgressViewModel.cs   # lépés-lista + spinner/állapot
│   │   │   ├── DiffViewModel.cs
│   │   │   ├── ReviewViewModel.cs     # szekciók, markdown
│   │   │   └── ConfigEditorViewModel.cs
│   │   ├── Views/                     # *.axaml párok
│   │   └── Assets/
│   └── CodeRev.Core.Tests/           # xUnit: EventParser, Runner (fake process)
└── docs/
    └── PLAN.md
```

A **mag (`CodeRev.Core`) UI-független és headless tesztelhető** – a process-spawn
és a protokoll-feldolgozás a legkockázatosabb rész, ezt fedjük tesztekkel.

---

## 7. Fő nézetek (MVVM)

### 7.1 Vázlatos elrendezés

```
┌─ coderev ───────────────────────────────────────────────────────────┐
│ Repo: ~/proj   Branch:[feature/login ▾]  Base:[origin/main ▾] Agent:[codex ▾] │
│ [⚙ Beállítások]                                   [▶ Review indítása] [■ Stop] │
├───────────────┬──────────────────────────────────────────────────────┤
│ Lépések       │  ┌─ Diff ─┬─ Review ─┬─ Napló ─┐                       │
│ ✓ Repo        │  │ a.go                                              │ │
│ ✓ Fetch       │  │  12 │ - old line                                  │ │
│ ✓ Diff  47h   │  │  12 │ + new line   ◀ talált probléma jelölés       │ │
│ ✓ Kontextus   │  │ ...                                               │ │
│ ⏳ AI review   │  └───────────────────────────────────────────────────┘ │
│   0:42 ▰▰▰▱   │  Review fül: Összegzés / Fő / Apró / Tesztek / Javaslat │
├───────────────┴──────────────────────────────────────────────────────┤
│ Állapot: AI dolgozik…  eltelt 0:42   |   prompt ~9.2 KB                │
└───────────────────────────────────────────────────────────────────────┘
```

### 7.2 Nézetek
- **RunSetup:** repo kiválasztás, branch/base **legördülő** (a git refekből),
  agent és nyelv választó, `--include-full-files` stb. kapcsolók.
- **Progress:** a lépés-lista élő állapotokkal (✓/✗/⏳), eltelt idő, mennyiségek.
- **Diff:** szintaxis-kiemelt diff, a változó sorok jelölve; a review-találatok
  ide horgonyozva.
- **Review:** a markdown renderelve, **szekciókra bontva** (Összegzés, Fő/Apró
  problémák súlyozott színnel, Tesztek, Javaslatok), kattintható `file:line`.
- **Napló:** a nyers eseményfolyam (diagnosztika).
- **ConfigEditor:** a `.coderev.json` grafikus szerkesztése (a CLI `init`
  megfelelője form alapon, validációval).

---

## 8. Mivel lehetne továbbfejleszteni – a GUI adta új lehetőségek

A grafikus felület minőségileg többet enged, mint a konzol. A javasolt
fejlesztések prioritás szerint:

### Áttekinthetőség (a fő érték)
- **Szekciókra bontott review** súlyozással: a Fő/Apró problémák külön, színkóddal
  (piros/sárga), összecsukható kártyákként – egy pillantásra látható a lényeg.
- **Navigálható találatok:** egy probléma kártyájára kattintva ugrás a diff
  megfelelő `file:line` helyére (és vissza).
- **Side-by-side diff** szintaxis-kiemeléssel; fájlfa a módosult fájlokkal,
  találat-számláló badge-ekkel fájlonként.
- **Szűrés/keresés** a review-ban (csak fő problémák, csak adott fájl, szövegkeresés).

### Interaktivitás, amit a konzol nem tud
- **Stop / Újrafuttatás** gombok (a Go `context` megszakításra ráépülve).
- **Inkrementális újrafuttatás** csak a kiválasztott fájlokra.
- **Több review összehasonlítása** (pl. két commit közti review egymás mellett).
- **Review-előzmények**: korábbi futások mentése, visszanézése, diff-elése.

### Konfiguráció és munkafolyamat
- **Grafikus konfig-szerkesztő** validációval (a kézi `.coderev.json` szerkesztés
  helyett); profilok/preszettek mentése (pl. „szigorú", „gyors").
- **Agent-kezelő**: ügynökök hozzáadása/szerkesztése (a `--agent-config`
  vizuálisan), kapcsolat-teszt gombbal.
- **Repo-/branch-böngésző**: refek legördülőből, nem gépeléssel; PR-szám alapján.

### Vizualizáció és export
- **Token-/költségbecslés** a prompt méretéből, futás előtt.
- **Súlyossági összegző** (hány fő/apró probléma) chart-ban.
- **Export**: review mentése Markdown / HTML / PDF formátumban, megosztáshoz.
- **Téma** (sötét/világos), betűméret, elrendezés-mentés.

### Integráció (későbbi körök)
- **GitHub/GitLab integráció:** a review-találatok **inline PR-kommentként**
  posztolása (a Go-motorban külön parancsként, a GUI-ból vezérelve).
- **Értesítések** (desktop notification) hosszú futás végén.
- **Watch mód:** új commitra automatikus újrafuttatás.

### Minőség / megbízhatóság
- **Bináris-felderítés és verzióellenőrzés**: a GUI ellenőrzi a `coderev` motor
  meglétét és verzióját, és figyelmeztet eltérésnél (offer: letöltés/PATH).
- **Hibatűrő protokoll-parser**: ismeretlen eseménytípusok átugrása előre-
  kompatibilitásért.

---

## 9. Cross-platform (Linux) megfontolások

- **Avalonia** natívan fut Linuxon (X11/Wayland); a publikálás
  `dotnet publish -r linux-x64` (és `win-x64`, `osx-arm64`) self-contained.
- **A motor bináris platformfüggő**: a megfelelő `coderev`/`coderev.exe`
  mellékelése vagy PATH-ból feloldása (a `BinaryLocator` feladata).
- **Útvonalak**: `Path.Combine`, nincs hardcode-olt elválasztó; a felhasználói
  konfig helye platformhelyesen (`XDG_CONFIG_HOME` vs `%APPDATA%`).
- **git**: feltételezzük a PATH-on (mint a CLI). Hiányát a GUI jelezze.
- **Csomagolás**: Linuxon AppImage/`.deb`/Flatpak; Windowson MSIX/`.exe`;
  macOS-en `.app` (jövőbeli kör).

---

## 10. Ütemterv (mérföldkövek)

1. **D0 – Go-szerződés:** `--format json` + `JSONReporter` a Go-projektben,
   az események NDJSON-sémájával + tesztekkel. ✅ **kész**
2. **D1 – Mag:** `CodeRev.Core` – `CoderevRunner` (spawn, cancel),
   `EventStreamReader`, `ReviewSession` fold, `BinaryLocator`; 15 xUnit teszt.
   ✅ **kész**
3. **D2 – Csontváz UI:** Avalonia app, RunSetup + élő lépés-lista + diff/review
   fülek + Stop. ✅ **kész (váz)**
4. **D3 – Diff + Review:** diff-nézet kiemeléssel, review szekciókra bontva,
   navigálható találatok. ⬜
5. **D4 – Konfig + agentek:** grafikus `.coderev.json` szerkesztő, agent-kezelő. ⬜
6. **D5 – Csiszolás:** előzmények (auto-mentés + visszatöltés), export
   (Markdown/HTML), világos/sötét téma, self-contained csomagolás
   (`publish.ps1`, RuntimeIdentifiers: win/linux/osx). ✅ **kész**

---

## 11. Nyitott kérdések (döntésre)

1. **Bináris-szállítás:** a `coderev` motort **mellékeljük** az appba
   (önállóság), vagy **PATH-ból** oldjuk fel (kisebb csomag, közös verzió)?
2. **Szekcionálás helye:** a markdown→szekciók bontást a **Go-motor** végezze
   (egységes a CLI-vel) vagy a **.NET-oldal** (rugalmasabb a GUI-nak)?
3. **Diff forrása:** a diffet a motor `diff` eseményként **küldje**, vagy a GUI
   külön `git diff`-fel **maga** állítsa elő (kevesebb adat a protokollon)?
4. **Markdown render lib:** `Markdown.Avalonia` elég, vagy saját renderelő kell a
   navigálható találatokhoz?
5. **Hatókör most:** csak a **terv** marad ebben a körben, vagy kezdjük a D0-D1
   implementációt (Go JSON-reporter + .NET mag)?

# coderev (Go) – Újratervezési terv

> Az új, Go nyelvű konzolos alkalmazás terve. Megőrzi a legacy
> működésének lényegét, javítja a [hibákat](02-hibak-es-hianyossagok.md), és új
> fő képességként **élő, konzolos visszajelzést** ad a PR-review folyamatáról.

## 1. Célok

1. **Funkcionális paritás** a legacy eszközzel (CLI opciók, config, agent-rendszer).
2. **A felsorolt hibák javítása** – különösen a destruktív checkout, a csendes
   fetch-hiba, a bájtszintű csonkolás és a stdin-duplikáció.
3. **Élő visszajelzési rendszer** a konzolon, amíg az AI dolgozik (lásd 6. fejezet).
4. **Egyetlen, statikusan linkelt bináris**, külső runtime nélkül (a legacy
   Python-telepítési bonyodalmai megszűnnek).
5. **Tesztelhető, modularizált** kódbázis.

## 2. Nem-célok (első kör)

- Nem cél a GitHub/GitLab API-integráció (komment-posztolás) – ez későbbi bővítés.
- Nem cél több AI-ügynök párhuzamos futtatása.
- Nem cél grafikus felület.

## 3. Technológiai döntések (javaslat)

| Terület | Javaslat | Indoklás |
|---|---|---|
| Go verzió | 1.22+ | `log/slog`, modern stdlib. |
| CLI keretrendszer | [`spf13/cobra`](https://github.com/spf13/cobra) | Alparancsok, help, shell-completion; iparági standard. |
| Konfig | `spf13/viper` **vagy** kézi merge | Viper kényelmes, de nehéz; a precedencia kézzel is jól kezelhető. |
| Git | A `git` CLI hívása `os/exec`-kel | Megegyezik a legacyvel; nincs cgo, nincs libgit2 függőség. |
| Konzol UI | [`bubbletea`](https://github.com/charmbracelet/bubbletea) + [`lipgloss`](https://github.com/charmbracelet/lipgloss) TUI-hoz, **vagy** könnyű spinner ([`briandowns/spinner`](https://github.com/briandowns/spinner)) | Lásd a 6.4 alternatíva-elemzést. |
| Tesztelés | stdlib `testing` + `testscript` | Git-műveletek temp-repón. |

> A keretrendszer-választás végleges eldöntéséhez lásd a dokumentum végén lévő
> **Nyitott kérdéseket**.

## 4. Javasolt projektstruktúra

```
coderev_v2/
├── cmd/
│   └── coderev/
│       └── main.go            # belépési pont, cobra root cmd
├── internal/
│   ├── config/               # CLI + env + JSON merge, precedencia
│   │   ├── config.go
│   │   └── config_test.go
│   ├── gitx/                 # git wrapper: diff, show, changed files (checkout NÉLKÜL)
│   │   ├── gitx.go
│   │   └── gitx_test.go
│   ├── diffparse/            # hunk-elemzés, kontextus-részletek
│   │   ├── diffparse.go
│   │   └── diffparse_test.go
│   ├── prompt/               # prompt-építés (sablonozható)
│   │   ├── prompt.go
│   │   └── prompt_test.go
│   ├── agent/                # AgentSpec, futtatás stdin|arg|file módban, streaming
│   │   ├── agent.go
│   │   └── agent_test.go
│   └── ui/                   # konzolos visszajelzési rendszer (progress, spinner, log)
│       ├── reporter.go
│       └── reporter_test.go
├── docs/
├── go.mod
└── README.md
```

## 5. Vezérlési folyam (a hibajavításokkal)

```
1. Beállítások feloldása        (config: CLI > env > JSON > default)
2. Repó gyökér ellenőrzése       git rev-parse --show-toplevel
3. Bázis fetch                   git fetch --prune <remote>   → hibát JELZI (R2)
4. Referenciák feloldása         base = merge-base(base, head) – checkout NÉLKÜL (R1)
5. Diff számítás                 git diff <base>...<head>
6. Módosult fájlok               git diff --name-only
7. Hunk-elemzés                  a TELJES diffből (csonkolás csak a prompthoz, B1)
8. Kontextus-részletek           git show <head>:<path> alapján (B3)
9. Dokumentumok + sablon         beolvasás, méretkorlátok runa-határon (R6)
10. Prompt összeállítás
11. Agent futtatás               streameléssel + timeouttal (R3, R4)
12. Kimenet                      stdout + opcionális --out
```

Kulcsváltozás: **nincs `git checkout`**. A diff és a fájltartalom is
referencia-alapú (`git diff`, `git show`), így a felhasználó munkamenete
érintetlen marad.

## 6. A konzolos visszajelzési rendszer (fő új feature)

Cél: a felhasználó **végig lássa, mi történik**, miközben az eszköz – és főleg az
AI-ügynök – dolgozik. A legacyben a futás teljes ideje néma volt.

### 6.1 Megjelenítendő információk

- **Lépés-állapotok** (lásd az 5. folyamatot), egyenként ✓/✗/⏳ jelzéssel.
- **Aktív lépés spinnere** és **eltelt idő** számláló.
- **Mennyiségi adatok:** módosult fájlok száma, diff mérete, hunkok száma,
  prompt mérete (tokens/bytes becslés).
- **Agent fázis:** „AI gondolkodik…", majd a válasz **élő streamelése**, ahogy
  érkezik (ha az ügynök stdout-ra folyamatosan ír).
- **Záró összegzés:** összes eltelt idő, kimenet helye, esetleges figyelmeztetések.

### 6.2 Példa konzol-kimenet (vázlat)

```
coderev v2.0.0 — PR review: feature/login  (base: origin/main)

  ✓ Repó ellenőrzése                                  (0.0s)
  ✓ Bázis fetch (origin)                              (1.3s)
  ✓ Diff számítása            12 fájl · 47 hunk · 38 KB (0.2s)
  ✓ Kontextus kinyerése       12 részlet               (0.1s)
  ✓ Prompt összeállítása      ~9 200 token             (0.0s)
  ⏳ AI review (codex)  ▰▰▰▰▱▱▱  elapsed 0:42  …

  ── Élő válasz ───────────────────────────────────────
  ## Summary
  A bejelentkezési folyamat refaktorálása rendben…
```

A futás végén a spinner-sorok véglegesednek (✓), és jön az összegzés:

```
  ✓ AI review (codex)                                 (1:07)

Kész — review kiírva: review.md   |   összesen 1:11
```

### 6.3 Architektúra: `Reporter` interfész

A UI-t egy interfész mögé tesszük, hogy (a) tesztelhető legyen, (b) TTY és
nem-TTY (pipe, CI) környezetben is helyesen viselkedjen, (c) a megjelenítési
mód cserélhető legyen.

```go
package ui

type Reporter interface {
    StepStart(name string)                 // új lépés kezdődik
    StepInfo(name, detail string)          // mennyiségi adat a lépéshez
    StepDone(name string, d time.Duration) // ✓
    StepFail(name string, err error)       // ✗
    Warn(msg string)                       // figyelmeztetés
    StreamChunk(p []byte)                  // agent kimenet darab (élő)
    Summary(total time.Duration, outPath string)
}
```

Két implementáció:
- **`TTYReporter`** – spinner, ANSI színek, élő frissítés (a 6.4 lib-bel).
- **`PlainReporter`** – sormintás, szín nélküli kiírás pipe/CI esetén
  (automatikus, ha `!isatty(stdout)` vagy `--no-progress`/`NO_COLOR`).

### 6.4 Megjelenítési könyvtár – döntés

| Opció | Előny | Hátrány |
|---|---|---|
| **A) `bubbletea` + `lipgloss`** | Teljes TUI, gazdag, élő layout, jól streamel. | Nagyobb függőség és komplexitás; az élő válasz + spinner együtt modellt igényel. |
| **B) `briandowns/spinner` + kézi ANSI** | Könnyű, kevés függőség, gyors. | A streamelő válasz és a spinner összehangolása kézi munka. |
| **C) Tiszta stdlib** (`\r`, ANSI escape kódok) | Nulla függőség. | Minden kézzel; cross-platform (Windows ANSI) buktatók. |

**Ajánlás:** kezdetben **(B)** – könnyű spinner + a `Reporter` absztrakció. Az
agent streamelt kimenetét a spinner **fölött** vagy **alatt** jelenítjük meg úgy,
hogy a streamelés idejére a spinner leáll, és a nyers szöveg folyik. Ha később
gazdagabb élmény kell, a `Reporter` mögött **(A)**-ra cserélhető a felület a hívó
kód módosítása nélkül.

### 6.5 Agent-streaming megvalósítása

A legacy a teljes kimenetet megvárta. Az új verzióban:

```go
cmd := exec.CommandContext(ctx, exe, args...)
stdout, _ := cmd.StdoutPipe()
cmd.Start()

var buf bytes.Buffer            // a teljes válasz fájlba/stdout-ra mentéshez
r := io.TeeReader(stdout, &buf) // egyszerre stream + gyűjtés
sc := bufio.NewReader(r)
for {
    chunk := make([]byte, 4096)
    n, err := sc.Read(chunk)
    if n > 0 {
        reporter.StreamChunk(chunk[:n])  // élő kiírás
    }
    if err != nil { break }              // EOF / hiba
}
cmd.Wait()
```

- A `context.WithTimeout` adja az időkorlátot (R3).
- `Ctrl+C` → `signal.NotifyContext` → tiszta leállás, a subprocess megölésével.
- A `buf` tartalma kerül a `--out` fájlba és/vagy a végső stdoutra.
- **Csak `stdin` módban** írunk stdin-t (B2 javítás).

## 7. Konfiguráció és kompatibilitás

- A `.coderev.json` formátum **változatlan marad** (visszafelé kompatibilis a
  legacy config fájllal).
- Ugyanazok a környezeti változók (`CODEREV_*`), kiegészítve a `--head-ref`
  env-jével (R5 javítás): `CODEREV_HEAD_REF`.
- Új beállítások:
  - `agent-timeout` (másodperc, default pl. 600) – R3.
  - `no-progress` (bool) – a sima kimenet kényszerítése.
  - `strict-fetch` (bool) – a fetch-hiba legyen végzetes – R2.
  - `dry-run` (CLI flag) – prompt kiírása agent-futtatás nélkül – K6.

### 7.1 `init` alparancs – konfigfájl generálása

A `coderev init [útvonal] [opciók]` parancs legenerálja a `.coderev.json`-t a
repo gyökerébe (vagy a megadott útvonalra). A megadott flagek értékei kerülnek a
fájlba, a többi kulcs az alapértelmezettet kapja. Tulajdonságok:

- **Kiszámítható kimenet:** az `init` szándékosan nem olvas be létező konfigot
  vagy `CODEREV_*` env-változót – csak a beépített defaultokból és a megadott
  flagekből dolgozik.
- **Nincs csendes felülírás** (K8 javítás): meglévő fájlt csak `--force` ír felül.
- A generált fájl kulcsai kebab-case-ek, így a normál betöltési út (loadFile)
  vissza tudja olvasni – ezt round-trip teszt is védi.

## 8. Tesztelési terv

- **`diffparse`**: rögzített diff-minták (új fájl, törlés, átnevezés, bináris,
  több hunk) → várt `Hunk` lista. Lefedi K5-öt.
- **`config`**: precedencia (CLI > env > JSON > default) táblázatos tesztekkel.
- **`gitx`**: ideiglenes git repó létrehozása a tesztben, valódi `git` hívással.
- **`agent`**: egy fake „echo"-szerű parancs mindhárom módban (`stdin|arg|file`);
  ellenőrzi, hogy `arg`/`file` módban NINCS stdin (B2).
- **`ui`**: `PlainReporter` kimenetének rögzítése bufferbe, formátum-ellenőrzés.

## 9. Megvalósítási ütemterv (javasolt mérföldkövek)

1. **M1 – Váz:** go modul, cobra root cmd, config-feloldás + tesztek.
2. **M2 – Git+diff:** `gitx` (checkout nélkül), `diffparse` + tesztek.
3. **M3 – Prompt+agent:** prompt-építés, agent-futtatás streameléssel, timeout.
4. **M4 – UI:** `Reporter`, TTY/Plain implementáció, integráció a folyamatba.
5. **M5 – Csiszolás:** `--dry-run`, `--version`, README, end-to-end teszt.

## 10. Nyitott kérdések (a felhasználó döntése)

1. **CLI keretrendszer:** `cobra` (alparancsok, bővíthető) vagy könnyű stdlib
   `flag`? A legacy egyetlen parancs – a `flag` is elég lehet.
2. **Konzol-UI mélysége:** könnyű spinner (B) vagy teljes TUI bubbletea-vel (A)?
3. **Agent-válasz megjelenítése:** nyers streamelés, vagy a végén megformázott
   (pl. markdown-renderelt) kimenet?
4. **Maradjon-e a magyar/angol** prompt és UI – legyen-e `--lang` kapcsoló?

# coderev – Roadmap / ötlettár

> **Nem kötelező terv.** Ez egy ötlet-gyűjtemény, hogy a lehetséges
> továbbfejlesztések megmaradjanak és priorizálhatók legyenek. Semmi sem
> „lefejlesztendő" — ami értékesnek tűnik, az emelhető ki belőle. A meglévő
> alapra (közös **motor** + **CLI** + **GUI**, NDJSON protokoll) épít.

## Jelmagyarázat

| Réteg | | Prioritás | | Becsült méret |
|---|---|---|---|---|
| ⚙ motor (közös) | | ★★★ nagy hatás | | S = kicsi |
| ⌨ CLI | | ★★ közepes | | M = közepes |
| 🖥 GUI | | ★ kényelmi/extra | | L = nagy |
| 🧪 minőség/infra | | | | |

Egy motor-szintű feature általában **mindkét felületnek** szól.

## Hol tartunk most (kiindulás)

Kész: a motor JSON-szerződése (`--format json`), CLI (review + `init`), GUI
(setup, élő lépések, színes diff, szekcionált review, config-szerkesztő,
előzmények, export md/html, téma, mappa-/branch-választó, Infó, ikon, verzió).
Minden réteg tesztelt. A roadmap innen indul.

---

## 1. Kiemelt ötletek (a legnagyobb megtérülés)

Ezek a „többi ötletet is felszabadító" tételek.

### 1.1 Strukturált találatok a motorból ⚙ ★★★ (M)
A motor a markdown review mellett kérjen az ügynöktől **gépi formátumú
találat-listát** is: `fájl · sor · súlyosság · üzenet` (JSON, a protokoll új
`finding` eseménye). Ez egyszerre nyit meg több irányt:
- 🖥 **navigálható találatok**: kártyára kattintva ugrás a diff `fájl:sor`
  helyére, inline jelölés a diffen;
- ⌨ **SARIF** kimenet (GitHub Code Scanning) és **küszöb-alapú kilépőkód**;
- minden további feature (PR-komment, „apply suggestion") erre épül.

### 1.2 CI-mód ⌨ ★★★ (S–M)
`--fail-on=major|minor`, gépi riport (JSON/SARIF), csendes mód → a `coderev`
review-kapuvá válik egy pipeline-ban. Függ az 1.1-től.

### 1.3 Apply suggestions / follow-up ⚙🖥 ★★ (L)
Az ügynök ne csak jelentsen: javasoljon **patcheket**, amelyek a working tree-re
alkalmazhatók; vagy lehessen **visszakérdezni** a review-ra (multi-turn). Ettől
lesz „asszisztens" a „jelentés" helyett.

### 1.4 PR-platform integráció ⚙🖥 ★★ (M–L)
`coderev review <PR#> --post` → találatok **inline PR-kommentként**
GitHub/GitLab-ra (`gh`/`glab` CLI-n át), a GUI-ból gombbal vezérelve.

---

## 2. Motor / közös mag ⚙

| Ötlet | Pri | Méret | Megjegyzés |
|---|---|---|---|
| Strukturált alparancsok: `refs --json`, `config get/set --json`, `diff --json` | ★★ | S | A GUI minden képessége valódi CLI-képesség lesz; scriptelhető |
| Inkrementális review (kiválasztott fájlok / utolsó óta változott) | ★★ | M | Gyorsabb, olcsóbb iteráció |
| Gyorsítótár commit-SHA alapján | ★ | M | Ne fusson le újra ugyanarra |
| Token-/költségbecslés futás előtt | ★★ | S | Figyelmeztetés nagy diffnél |
| `.coderevignore` (generált/lock/vendor kihagyása) | ★★ | S | Tisztább prompt, kevesebb zaj |
| Forrás-rugalmasság: staged / working tree / commit-range / PR | ★★ | M | Nem csak ág |
| Repo-tudatos prompt (nyelv/keretrendszer + konvenciók) | ★ | M | Jobb review-minőség |
| Több ügynök + a review-k összevetése | ★ | M | „Második vélemény" |
| Protokoll-verzió egyeztetés motor ↔ GUI | ★ | S | Kompatibilitás-ellenőrzés |

## 3. CLI ⌨

| Ötlet | Pri | Méret | Megjegyzés |
|---|---|---|---|
| Shell-completion (bash/zsh/fish/pwsh) + man page | ★ | S | Kényelmi |
| `--watch`: új commitra auto-futtatás | ★ | M | Folyamatos visszajelzés |
| Csomagolás: Homebrew / Scoop / `.deb` / `go install` | ★★ | M | Könnyű telepítés |
| Batch-mód több ágra | ★ | M | |
| Profilok: `--profile strict\|fast` | ★ | S | Előre definiált csomagok |

## 4. GUI 🖥

| Ötlet | Pri | Méret | Megjegyzés |
|---|---|---|---|
| Navigálható találatok + inline diff-kommentek | ★★★ | M | Az 1.1-re épül; GitHub-szerű |
| Valódi markdown-renderelés a review-fülön | ★★ | S | Markdown.Avalonia |
| Side-by-side diff + szűrés/keresés | ★ | M | |
| Legutóbbi repók, base auto-detektálás (`origin/HEAD`), drag-and-drop | ★★ | S | Munkafolyamat-gyorsítók |
| Utolsó beállítások megjegyzése (app-settings) | ★★ | S | Ne kelljen újra beállítani |
| Fájlonkénti újrafuttatás (pipák a fájl-listában) | ★ | M | Inkrementális review-ra épít |
| Két review összevetése (előzmény-diff) | ★ | M | |
| Agent-teszt gomb (elérhetőség/verzió) | ★★ | S | Gyors hibakeresés |
| Értesítések (desktop toast), PDF-export, vágólap | ★ | S | |
| Akadálymentesség (billentyű, képernyőolvasó), betűméret, akcentszín | ★ | M | |
| PR-posztolás a GUI-ból (auth) | ★★ | L | Az 1.4-re épül |

## 5. Minőség / infrastruktúra 🧪

| Ötlet | Pri | Méret | Megjegyzés |
|---|---|---|---|
| CI-pipeline (GitHub Actions): Go + .NET build, tesztek, artifact-ok | ★★ | M | Megbízhatóság |
| Integrációs teszt: a .NET a *valódi* motort futtatja temp repón | ★★ | S | A protokoll end-to-end fedése |
| Aláírt bináris / notarizáció (Win code signing, macOS notarize) | ★ | M | Terjesztéshez |
| GUI auto-update | ★ | L | |
| Opt-in telemetria a hibákról | ★ | M | |

## 6. Review-minőség / AI 🤖

| Ötlet | Pri | Méret | Megjegyzés |
|---|---|---|---|
| Jobb promptok: tesztek, kapcsolódó kód, projekt-konvenciók beemelése | ★★ | M | A kimenet minősége |
| Egyedi szabály-/checklist-készletek projektenként | ★ | M | |
| Kockázat-pontozás / összegző sok fájlnál | ★ | M | |
| Többnyelvűség a hu/en-en túl (teljes i18n) | ★ | M | |

---

## 7. Laza sorrend-javaslat (nem kötelező)

Csak egy lehetséges útvonal; bármi kihagyható vagy átugorható.

1. **Alapozó kör:** strukturált találatok (1.1) → ez nyitja a többit.
2. **CLI-erő:** CI-mód + SARIF (1.2), csomagolás (3), `.coderevignore` (2).
3. **GUI-élmény:** navigálható találatok + markdown-render + utolsó beállítások (4).
4. **Asszisztens-szint:** apply suggestions / follow-up (1.3), PR-integráció (1.4).
5. **Érettség:** CI-pipeline, integrációs teszt, aláírás, auto-update (5).

## 8. Függőségi térkép (röviden)

```
Strukturált találatok (1.1) ──┬──▶ SARIF + CI-mód (1.2, CLI)
                              ├──▶ Navigálható találatok + inline diff (GUI)
                              └──▶ PR-komment posztolás (1.4) ──▶ GUI PR-posztolás
Inkrementális review (2) ─────────▶ Fájlonkénti újrafuttatás (GUI)
Strukturált alparancsok (2) ──────▶ GUI „minden a motoron át" őszinteség
```

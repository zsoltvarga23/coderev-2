# coderev (legacy) – Ismert hibák és hiányosságok

> A `coderev/__init__.py` átolvasása alapján azonosított problémák. Ezeket a Go
> újraírásban javítani / elkerülni érdemes. A súlyosság: 🔴 hiba, 🟠 kockázat,
> 🟡 minőség/UX.

## 🔴 Funkcionális hibák

### B1. A diffet bájtszinten csonkolja a hunk-elemzés ELŐTT
[`__init__.py:611-618`](../coderev-legacy/coderev/__init__.py#L611)

```python
diff_bytes = diff_text.encode("utf-8", errors="replace")
if len(diff_bytes) > args.max_diff_bytes:
    diff_text = diff_bytes[: args.max_diff_bytes].decode("utf-8", errors="replace") + ...
...
hunks = parse_changed_hunks(diff_text)
```

A csonkolás egy tetszőleges bájthatáron vág, ami **félbevághat egy hunk-fejlécet
vagy egy `+++ b/` sort**, így a `parse_changed_hunks()` hibás vagy hiányos
kontextust ad. A csonkolt diff a prompt minőségét is rontja.
**Javítás (Go):** sor-/hunk-határon csonkolj, és külön kezeld a diffet a
promptban vs. az elemzéshez.

### B2. `arg` és `file` módban a prompt a stdin-re IS rámegy
[`__init__.py:435-464`](../coderev-legacy/coderev/__init__.py#L435)

Mindkét ágban `input=prompt` szerepel, pedig `arg` módban az argumentumban,
`file` módban a fájlban van a prompt. Egyes CLI-k összezavarodhatnak a duplikált
bemenettől, vagy feleslegesen blokkolódhatnak a stdin olvasáskor.
**Javítás (Go):** csak `stdin` módban adj stdin-t; a másik kettőben ne.

### B3. A kontextus a working tree-ből olvas, nem a `head-ref`-ből
[`__init__.py:222`](../coderev-legacy/coderev/__init__.py#L222)

A `build_context_snippets()` az `abs_path.read_text()`-tel a **lemezen lévő
aktuális** fájlt olvassa. Ha a `--head-ref` nem a kicheckoutolt working tree
(pl. egy konkrét commit hash, vagy van uncommitted módosítás), a kontextus
**nem a review-zott állapotot** tükrözi.
**Javítás (Go):** a tartalmat `git show <head-ref>:<path>` paranccsal olvasd.

### B4. Holt kód: `obey_doc_default`
[`__init__.py:530-532`](../coderev-legacy/coderev/__init__.py#L530)

Kiszámolódik egy `obey_doc_default` változó, de az argparse `default=[]`-t kap,
és a config-értékek külön, a [602-605. sorban](../coderev-legacy/coderev/__init__.py#L602)
fűződnek hozzá. A `530-532` blokk **soha nem használt**.

### B5. Holt kód: `which_or_die()`
[`__init__.py:345-348`](../coderev-legacy/coderev/__init__.py#L345)

Definiált, de **sehol nem hívott** függvény; a `run_agent()` saját
`shutil.which` ellenőrzést használ.

## 🟠 Kockázatok és mellékhatások

### R1. Destruktív `git checkout` a felhasználó munkamenetében
[`__init__.py:118-131`](../coderev-legacy/coderev/__init__.py#L118)

A `checkout_branch()` **átváltja a felhasználó working tree-jét** a review-zott
ágra, és a futás végén **nem állítja vissza** az eredeti ágat. Uncommitted
módosítások esetén a checkout megszakadhat, vagy meglepő állapotot hagy hátra.
**Javítás (Go):** ne válts ágat. Dolgozz `git diff <base>...<head>` és
`git show` alapján checkout nélkül; vagy állítsd vissza az eredeti `HEAD`-et.

### R2. A `fetch_base()` minden hibát csendben elnyel
[`__init__.py:107-115`](../coderev-legacy/coderev/__init__.py#L107)

```python
except subprocess.CalledProcessError:
    pass
```

Hálózati hiba, hibás remote, auth probléma esetén **semmilyen jelzés** nincs, és
a diff egy elavult bázishoz készül. A felhasználó téves review-t kap.
**Javítás (Go):** legalább figyelmeztetést írj ki; tedd `--strict` opcióval
hibává.

### R3. Nincs időtúllépés az agent futtatásán
[`__init__.py:422-464`](../coderev-legacy/coderev/__init__.py#L422)

Ha az AI-ügynök beragad, a folyamat **örökre függ**, megszakítási lehetőség és
visszajelzés nélkül.
**Javítás (Go):** `context.WithTimeout` + konfigurálható timeout, és tiszta
megszakítás `Ctrl+C`-re.

### R4. A teljes agent-kimenet memóriában gyűlik, nincs streaming
[`__init__.py:478-484`](../coderev-legacy/coderev/__init__.py#L478)

A `subprocess.run(... stdout=PIPE)` megvárja a teljes választ, majd egyszerre
írja ki. A felhasználó **a futás teljes ideje alatt semmit sem lát** – éppen ez
az, amit az új verzióban élő visszajelzéssel kell megoldani (lásd
[`03-go-ujratervezes.md`](03-go-ujratervezes.md)).

### R5. `--head-ref`-nek nincs környezeti változója
[`__init__.py:526-528`](../coderev-legacy/coderev/__init__.py#L526)

A `--base-ref` env változóból is jöhet, de a `--head-ref` nem – aszimmetrikus,
és a táblázatból is kilóg.

### R6. Bájtszintű középső csonkolás multi-byte karaktereknél
[`__init__.py:199-203`](../coderev-legacy/coderev/__init__.py#L199)

A `get_file_content()` a fájl közepén bájthatáron vág (`head`/`tail`), ami
multi-byte UTF-8 karaktert tör el (a `errors="replace"` ezt csak elfedi � jellel).
**Javítás (Go):** rúna-/sorhatáron csonkolj.

## 🟡 Minőség, UX, karbantarthatóság

- **K1. Nincs valós idejű haladásjelzés.** Se spinner, se lépés-kiírás, se
  eltelt idő. (Az új fő feature.)
- **K2. Nincs egyetlen teszt sem.** Se a diff-elemzésre, se a config-feloldásra,
  se az agent-indításra.
- **K3. Minden egy fájlban** (~660 sor). Nincs modularizáció (git, config,
  prompt, agent, ui szétválasztás).
- **K4. A prompt szövege és a kimeneti elvárások hardcode-oltak**, csak az angol
  nyelv támogatott.
- **K5. A `parse_changed_hunks()` nem kezeli explicit módon** a törölt fájlokat
  (`+++ /dev/null`), átnevezéseket, bináris fájlokat – ezekre rossz kontextust
  adhat.
- **K6. Nincs `--version`** és nincs `--dry-run` (a prompt megtekintése
  agent-futtatás nélkül).
- **K7. A `list_changed_files()` nincs külön hibakezelve** – nyers
  `CalledProcessError` szivároghat ki.
- **K8. Csendes felülírás:** az `--out` fájlt figyelmeztetés nélkül felülírja.

## Összefoglaló prioritás a Go-verzióhoz

| Prioritás | Tételek |
|---|---|
| Must (hibajavítás) | B1, B2, B3, R1, R2, R3 |
| Should (UX/új feature) | R4, K1, K6 |
| Should (minőség) | B4, B5, K2, K3 |
| Nice to have | R5, R6, K4, K5, K7, K8 |

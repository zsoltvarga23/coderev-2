# UI koncepció B – Oldalsáv-navigáció (jövőbeli irány)

> **Státusz:** dokumentált **alternatíva**, jövőbeli referenciának. A rövid távon
> választott irány a **Koncepció A** („letisztult workspace") – a mostani
> egyablakos elrendezés evolúciója. Ez a dokumentum a **B koncepciót** rögzíti
> arra az esetre, ha később a nagyobb, app-szerű átszervezés mellett döntünk.
>
> Kapcsolódó: [PLAN.md](PLAN.md) (alaptervezés), valamint a már meglévő
> `RecentRepositoriesStore` (recent-repos adatréteg), amelyre az itt leírt
> Starter/Home nézet ráül.

## 1. Cél és kontextus

A jelenlegi GUI egy sűrű, egyablakos elrendezés: a tetején minden setup-mező és
egy sornyi szöveges gomb, alatta balra a lépéslista, jobbra egy `TabControl`
(Review / Diff / History / Log). Ez működik, de ahogy az app nő (Settings,
Starter screen, esetleg több nézet), a felső sáv zsúfolttá válik.

A **B koncepció** egy modern, „termék"-szerű elrendezést ad: bal oldali
**navigációs rail**, fent **kontextus-sáv**, és egy tágas, fókuszált
tartalom-terület. Jobban skálázódik, és természetes otthont ad a későbbi
**Starter/Home** nézetnek.

## 2. Áttekintés

- **Bal oldali nav-rail:** ikon + címke nézetenként (Home/Review, Diff, History,
  Settings). Az aktív nézet akcentussal kiemelve. Alul/fent az elsődleges
  **Run review** akció.
- **Kontextus-sáv (fent):** az aktuális repó neve, a branch/base/agent chipek,
  jobbra a téma-/nyelvváltó és a feltételes **Frissítés** gomb.
- **Lépés-csík:** vízszintes, kcompakt állapot-jelző (repo → base → diff →
  prompt → ai review) a tartalom tetején.
- **Tartalom-terület:** az aktív nézet (pl. Review a súlyozott kártyákkal)
  tágasan, fókuszáltan.

## 3. Elrendezés (wireframe)

```
┌────────────┬──────────────────────────────────────────────────────────┐
│ ⎇ coderev  │  📁 coderev-2   ⎇ feature/login   base: origin/main  …     │  ← kontextus-sáv
│            │                                   ⬆ Update  ◐ téma  🌐 nyelv│
│ ● Review   ├──────────────────────────────────────────────────────────┤
│ ○ Diff     │  ✓ repo → ✓ base → ✓ diff(3) → ✓ prompt → ⟳ ai review     │  ← lépés-csík
│ ○ History  ├──────────────────────────────────────────────────────────┤
│ ○ Settings │                                                            │
│            │   ┌─ Summary ──────────────────────────────────────────┐   │
│            │   │ The login refactor is solid…                       │   │
│            │   └────────────────────────────────────────────────────┘   │
│            │   ┌─ Major issues ─────────────────────────────────────┐   │
│            │   │ getHash doesn't handle the null case…              │   │
│ ┌────────┐ │   └────────────────────────────────────────────────────┘   │
│ │ ▶ Run  │ │   ┌─ Suggestions ──────────────────────────────────────┐   │
│ └────────┘ │   │ Add a unit test for the empty-token path.          │   │
│            │   └────────────────────────────────────────────────────┘   │
└────────────┴──────────────────────────────────────────────────────────┘
   nav-rail                    tartalom-terület
```

## 4. Navigációs modell

Egy `SelectedView` enum vezérli, melyik nézet aktív; a rail kiválasztása ezt
állítja, a tartalom-terület ennek megfelelő nézetet jelenít meg.

| Nézet | Tartalom |
|---|---|
| **Home / Starter** *(későbbi)* | Recent-repó kártyák (4-5 + „Több…") + „Repository megnyitása". Lásd lent. |
| **Review** | A súlyozott review-kártyák (Summary/Major/Minor/Tests/Suggestions) a `MarkdownView`-val renderelve. |
| **Diff** | Fájllista + színezett, sorszámozott diff (átméretezhető oszlopok, téma-érzékeny színek – ezek már megvannak). |
| **History** | Korábbi futások repó-chippel és repó-szerinti szűrővel (már megvan). |
| **Settings** | A `.coderev.json` szerkesztő (ma külön ablak – ide beágyazható). |

A setup-mezők (repo/branch/base/agent/lang/dry-run) a **kontextus-sávban** vagy
egy összecsukható panelben élnek, nem külön „setup képernyőn”.

## 5. Vizuális nyelv

- **Akcentus-szín:** egységes, téma-érzékeny brush az elsődleges akcióhoz és az
  aktív nav-elemhez (a meglévő `App.axaml` light/dark theme dictionary mintára,
  mint a diff/markdown színek).
- **Ikonok:** beágyazott ikon-font (MIT/OFL, pl. Tabler) – nem emoji.
- **Kártyák:** lekerekített, finom keret, színes fejléc súlyosság szerint
  (szemantikus színek, sötét módban is olvasható).
- **Üres állapotok:** minden nézethez barátságos placeholder (pl. Review review
  előtt).

## 6. Avalonia megvalósítási vázlat

Standard MVVM, a meglévő `CodeRev.App`-ra építve – nincs új keretrendszer.

- **Rail:** egy `ListBox` (vagy `ToggleButton`-lista) `SelectedView`-hoz kötve;
  item-template = ikon + címke; az aktív elem stílusa akcentussal.
- **Tartalom-csere:** egy `ContentControl`, aminek a `Content`-je a kiválasztott
  nézet. A nézeteket érdemes külön `UserControl`-okká kiszervezni
  (`ReviewView`, `DiffView`, `HistoryView`, `SettingsView`, `HomeView`), mindegyik
  saját (vagy megosztott) ViewModellel. Ez a mostani `MainWindow.axaml`
  feldarabolása – a logika a `MainWindowViewModel`-ből al-ViewModelekbe kerül.
- **Kontextus-sáv és lépés-csík:** a meglévő `Steps`/setup state-re kötve, csak
  más vizuális megjelenítéssel (vízszintes).
- **Navigáció:** `[ObservableProperty] SelectedView _selectedView;` +
  `DataTemplate`-ek nézetenként (vagy egy `ViewLocator`).

### Migráció a jelenlegiből / Koncepció A-ból

A Koncepció A már behozza a **közös alapokat** (ikon-font, akcentus-szín,
tokenek, üres állapotok, súlyozott kártyák). A B ezekre épülve „csak" az
**elrendezést és a navigációt** szervezi át:

1. A nézettartalmak kiszervezése `UserControl`-okká (a legnagyobb lépés).
2. A `MainWindowViewModel` felbontása nézet-ViewModelekre.
3. A rail + `ContentControl` + `SelectedView` bevezetése.
4. A setup áthelyezése a kontextus-sávba / összecsukható panelbe.

## 7. Starter / Home nézet (a recent-repos store-ra építve)

A már meglévő `RecentRepositoriesStore` (MRU lista a megnyitott repókról)
közvetlenül táplálja a Home nézetet:

- **Recent kártyák:** 4-5 legutóbbi repó (név + halvány út + relatív idő), egy
  „Több…" gombbal a teljes listához, és egy ✕-szel az eltávolításhoz
  (`RecentRepositoriesStore.Remove` már létezik).
- **„Repository megnyitása"** gomb (mappa-választó), pont mint ma.
- Választás után a `SelectedView` Review-ra vált, a kontextus-sáv feltöltődik.

Így a Home nézet nem igényel új adatréteget – csak egy új `UserControl` a
meglévő store fölé.

## 8. Nyitott kérdések

- A setup (repo/branch/…) a kontextus-sávban férjen el, vagy egy összecsukható
  „Setup" panelben? (Sok mező – lehet, hogy a panel tisztább.)
- A Settings (`.coderev.json`) maradjon külön ablak, vagy beágyazott nézet legyen?
- A Log nézet külön rail-elem legyen, vagy egy alsó, összecsukható konzol?
- Reszponzív viselkedés: keskeny ablaknál a rail ikon-only módba csukódjon?

## 9. Döntés

A **Koncepció A** a választott rövid távú irány (kisebb kockázat, gyors vizuális
nyereség, illeszkedik a jelenlegi architektúrához). A **B** akkor jön képbe, ha
az app több nézettel bővül (Starter/Home, Settings beágyazás) – ekkor a fenti
vázlat a kiindulópont.

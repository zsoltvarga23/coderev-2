# coderev v2 – Dokumentáció

Ez a mappa a `coderev` eszköz **Go nyelvű újraírásának** alapdokumentációja. A
forrás a `coderev-legacy/` Python-projekt, amelyet áttekintettünk, dokumentáltunk
és továbbfejlesztési tervvel láttunk el.

## Tartalom

1. **[01-legacy-attekintes.md](01-legacy-attekintes.md)** – A meglévő Python
   eszköz működése: adatfolyam, CLI opciók, konfiguráció, agent-rendszer, prompt.
2. **[02-hibak-es-hianyossagok.md](02-hibak-es-hianyossagok.md)** – A kódból
   azonosított hibák, kockázatok és minőségi hiányosságok, prioritással.
3. **[03-go-ujratervezes.md](03-go-ujratervezes.md)** – A Go-s alkalmazás terve:
   projektstruktúra, javított vezérlési folyam, és a **konzolos visszajelzési
   rendszer** részletes terve.

## Dióhéjban

A `coderev` egy CLI eszköz, amely egy git ág diffjéből AI-alapú PR review-t
készít egy konfigurálható külső AI-ügynökkel (Codex, Copilot, vagy bármilyen
CLI). Az új Go verzió fő céljai:

- **Hibajavítás** – nincs destruktív checkout, nincs csendes fetch-hiba, helyes
  diff-csonkolás, referencia-alapú kontextus.
- **Élő konzolos visszajelzés** – lépés-állapotok, spinner, eltelt idő és az AI
  válaszának streamelése munka közben (a legacy néma futása helyett).
- **Egyetlen bináris**, moduláris és tesztelt kódbázis.

A megvalósítás megkezdése előtti döntésekhez lásd a
[03-go-ujratervezes.md](03-go-ujratervezes.md) **„Nyitott kérdések"** szakaszát.

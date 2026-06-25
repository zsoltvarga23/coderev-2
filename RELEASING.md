# Kiadás készítése (Releasing)

A kiadásokat a [`.github/workflows/release.yml`](.github/workflows/release.yml)
GitHub Actions workflow építi és tölti fel. Egyetlen verziószám-forrás van: a
repó gyökerében lévő **`VERSION`** fájl. Ebből olvas a Go CLI (build-időben,
`-ldflags "-X main.version=..."`) és a .NET GUI is
([`coderev-desktop/Directory.Build.props`](coderev-desktop/Directory.Build.props)).

## Lépések

1. **Verzió emelése:** írd át a `VERSION` fájlt (pl. `1.1.0`). A workflow
   leellenőrzi, hogy a tag pontosan ezzel egyezik-e.
2. (Ajánlott) frissítsd a `CHANGELOG.md`-t, ha van.
3. Commitold a változást a `main`-re.
4. Tageld és pushold:
   ```bash
   git tag v1.1.0      # = "v" + a VERSION tartalma
   git push origin v1.1.0
   ```
5. A workflow lefut és **draft** GitHub release-t hoz létre a felépített
   assetekkel. **Nézd át, majd kézzel publikáld** (Releases → a draft →
   *Publish release*). A felhasználók auto-update-je csak publikált (nem draft,
   nem prerelease) kiadást lát.

## Mit épít a workflow

| Komponens | Asset(ek) |
|---|---|
| CLI (Go) | `coderev-windows-amd64.exe`, `coderev-linux-amd64` |
| Integritás | `checksums.txt` (SHA-256; a `coderev update` ezt ellenőrzi) |
| GUI (Velopack) | `CodeRev-win-Setup.exe`, `*.AppImage`, `*-full.nupkg`, `assets.*.json` |

A GUI assetek per-platform **channel**-enként (`win`, `linux`) készülnek; ezekből
táplálkozik a Velopack in-app frissítés. A CLI binárisokból a self-update.

## Megjegyzések

- **Sorrend:** a per-OS build job-ok sorosan futnak (`max-parallel: 1`), hogy
  ugyanabba a draftba fűzzék az assetjeiket (`vpk upload --merge`, `--publish`
  nélkül → draft marad).
- **Kódaláírás:** jelenleg nincs (belsős használat). Aláíráshoz add meg a
  `vpk pack --signParams ...` paramétereket és a megfelelő secreteket; a Go CLI
  signtoolozható build után. SmartScreen addig figyelmeztethet.
- **Tesztelés tag nélkül:** a [`ci.yml`](.github/workflows/ci.yml) minden
  push/PR esetén buildel és tesztel (Go + .NET), de nem ad ki semmit.
- **Privát/fork forrás:** a frissítés forrása felülírható —
  `CODEREV_UPDATE_URL` (GUI) / `CODEREV_UPDATE_REPO` (CLI).

using System.ComponentModel;

namespace CodeRev.App.Localization;

/// <summary>
/// Lightweight, dependency-free UI localization with runtime language switching.
/// XAML binds to the string indexer via the <c>{loc:Tr Key}</c> markup extension;
/// changing the language raises a blanket <see cref="PropertyChanged"/> so every
/// bound label refreshes live. View models use <see cref="T"/> for dynamic text.
/// Default language is English.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private string _lang = "en";

    public string Language => _lang;

    public string this[string key] =>
        (_lang == "hu" ? Hu : En).TryGetValue(key, out var v) ? v : key;

    /// <summary>Localized string with optional <see cref="string.Format"/> args.</summary>
    public string T(string key, params object[] args)
    {
        var s = this[key];
        return args.Length > 0 ? string.Format(s, args) : s;
    }

    public void SetLanguage(string lang)
    {
        if (lang is not ("en" or "hu") || lang == _lang) return;
        _lang = lang;
        // Empty name = "all properties changed" → refreshes every [key] binding.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    public void Toggle() => SetLanguage(_lang == "en" ? "hu" : "en");

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---- translations ----------------------------------------------------

    private static readonly Dictionary<string, string> En = new()
    {
        // Main window — setup
        ["RepoLabel"] = "Repo:",
        ["RepoWatermark"] = "git repository path",
        ["BrowseTip"] = "Browse folder / repository",
        ["RecentRemoveTip"] = "Remove from recent",
        ["BranchLabel"] = "Branch:",
        ["BaseLabel"] = "Base:",
        ["AgentLabel"] = "Agent:",
        ["ReviewLangLabel"] = "Review lang:",
        ["DryRun"] = "Dry-run",
        // Main window — toolbar
        ["InfoBtn"] = "Info",
        ["SettingsBtn"] = "Settings",
        ["ExportMdTip"] = "Export review to Markdown",
        ["ExportHtmlTip"] = "Export review to HTML",
        ["ThemeBtn"] = "Theme",
        ["UiLangToggle"] = "Magyar",
        ["UpdateBtn"] = "Update",
        ["UpdAvailableTip"] = "Update available: v{0} — click to install",
        ["RunBtn"] = "Start review",
        ["StopBtn"] = "Stop",
        // Update flow
        ["StUpdDownloading"] = "Downloading update… {0}%",
        ["StUpdRestarting"] = "Update downloaded — restarting…",
        ["StUpdError"] = "Update error: {0}",
        ["UpdDlgTitle"] = "Update available",
        ["UpdDlgBody"] = "Version {0} is available (you have v{1}).\nDownload and install now? The app will restart.",
        ["UpdYes"] = "Update & restart",
        ["UpdNo"] = "Later",
        // Main window — content
        ["StepsHeader"] = "Steps",
        ["TabReview"] = "Review",
        ["TabDiff"] = "Diff",
        ["TabHistory"] = "History",
        ["TabLog"] = "Log",
        ["ReviewEmptyTitle"] = "No review yet",
        ["ReviewEmptyHint"] = "Start a review to see the AI's findings here.",
        ["DiffEmptyTitle"] = "No diff yet",
        ["DiffEmptyHint"] = "Start a review to see the changes here.",
        ["ShowAllFiles"] = "All",
        ["DiffResizeTip"] = "Drag to resize the file/code columns",
        ["HistoryHint"] = "Past runs (click to reload)",
        ["HistRepoFilter"] = "Repository:",
        ["HistAllRepos"] = "All repositories",
        ["HistUnknownRepo"] = "(unknown)",
        // Status messages
        ["StReady"] = "Ready.",
        ["StRunning"] = "Running…",
        ["StDone"] = "Done.",
        ["StCancelled"] = "Cancelled.",
        ["StError"] = "Error: {0}",
        ["StNeedRepoBranch"] = "Enter a repository path and a branch.",
        ["StSelectAgentLang"] = "No .coderev.json in this repo — please select an agent and a review language.",
        ["StNotRepo"] = "The selected folder is not a git repository.",
        ["StBranchesLoaded"] = "{0} local branches loaded.",
        ["StConfigImported"] = "  (.coderev.json imported)",
        ["StReviewOf"] = "Review: {0}  (base: {1})",
        ["MetaFormat"] = "{0} files · {1} hunks · prompt ~{2} B",
        ["StSummaryOut"] = "Done — written to: {0}  ({1} ms)",
        ["StSummary"] = "Done  ({0} ms)",
        ["StHistoryLoaded"] = "History entry loaded: {0}",
        ["StNothingExport"] = "Nothing to export — run a review first.",
        ["StExported"] = "Exported: {0}",
        // About window
        ["Close"] = "Close",
        ["AboutSubtitle"] = "AI-powered Pull Request review — version {0}",
        ["AboutWhatHdr"] = "What is this?",
        ["AboutWhatBody"] = "A tool that sends a git branch's changes (the diff) to an AI agent and returns a code review. The heavy lifting (git, diff, prompt, running the agent) is done by the coderev engine; this GUI shows the process, the diff and the review clearly.",
        ["AboutHowHdr"] = "How to use",
        ["AboutHow1"] = "1.  Open a git repository with the 📂 button.",
        ["AboutHow2"] = "2.  Pick a branch (autocomplete helps from local branches, or type it) and a base reference (e.g. origin/main).",
        ["AboutHow3"] = "3.  Choose an agent and language, then press ▶ Start review.",
        ["AboutHow4"] = "The steps, the diff and the AI response appear live. The review can be exported (.md / .html), past runs are reloadable from the History tab, and the theme can be toggled.",
        ["AboutAiHdr"] = "Using it with AI",
        ["AboutAiBody"] = "The AI is provided by an external command-line \"agent\" that the engine calls, passing it the prompt (the diff and context). Built-in agents:",
        ["AboutAiCodex"] = "•  codex — OpenAI Codex CLI",
        ["AboutAiCopilot"] = "•  copilot — GitHub Copilot CLI",
        ["AboutAiClaude"] = "•  claude — Claude Code CLI",
        ["AboutAiCustom"] = "A custom agent can also be defined in the ⚙ Settings window (agent-config JSON: command + mode).",
        ["AboutNeedHdr"] = "What you need",
        ["AboutNeedGit"] = "•  git on PATH (to analyze the repo).",
        ["AboutNeedAgent"] = "•  The chosen agent CLI installed and signed in, on PATH. Note: for Claude you need the standalone Claude Code CLI (npm i -g @anthropic-ai/claude-code) — the editor plugin alone is not enough.",
        ["AboutNeedEngine"] = "•  The coderev engine binary: the CODEREV_BIN environment variable, or bundled next to the app, or on PATH.",
        ["AboutNeedTip"] = "Tip: with the Dry-run checkbox the whole flow runs without AI (and cost) — you see the steps and the diff, only the review stays empty. Good for a first try.",
        // Config editor
        ["CfgHeader"] = ".coderev.json settings",
        ["CfgReload"] = "Reload",
        ["CfgSave"] = "💾 Save",
        ["CfgBaseRef"] = "Base ref",
        ["CfgHeadRef"] = "Head ref",
        ["CfgLang"] = "Language",
        ["CfgOut"] = "Output file (out)",
        ["CfgOutWatermark"] = "e.g. review.md (empty = console only)",
        ["CfgAgent"] = "Agent",
        ["CfgBuiltin"] = "Built-in:",
        ["CfgUseCustom"] = "Use a custom agent (overrides the built-in one)",
        ["CfgCustomHint"] = "Custom agent (JSON): e.g. {\"name\":\"mycli\",\"cmd\":[\"mycli\",\"--in\",\"{prompt_file}\"],\"mode\":\"file\"}",
        ["CfgIncludeFull"] = "Include full contents of changed files (include-full-files)",
        ["CfgStrictFetch"] = "Failed fetch is fatal (strict-fetch)",
        ["CfgNoProgress"] = "Disable spinner (no-progress)",
        ["CfgContextLines"] = "Context lines",
        ["CfgAgentTimeout"] = "Agent timeout (s)",
        ["CfgTemplate"] = "Template",
        ["CfgTemplateWatermark"] = "e.g. review_template.md",
        ["CfgObeyDoc"] = "Docs to obey (obey-doc) — one per line",
        // Config editor status
        ["CfgStNoRepo"] = "No folder/repository open — saving is unavailable. Open one in the main window (📂).",
        ["CfgStLoaded"] = "Loaded: {0}",
        ["CfgStDefaults"] = "No .coderev.json yet — default values.",
        ["CfgStInvalidLang"] = "Language must be hu or en.",
        ["CfgStInvalidJson"] = "The custom agent is not valid JSON.",
        ["CfgStSaved"] = "Saved: {0}",
        ["CfgStSaveError"] = "Save error: {0}",
    };

    private static readonly Dictionary<string, string> Hu = new()
    {
        ["RepoLabel"] = "Repó:",
        ["RepoWatermark"] = "git repó útvonala",
        ["BrowseTip"] = "Mappa / repository tallózása",
        ["RecentRemoveTip"] = "Eltávolítás a legutóbbiakból",
        ["BranchLabel"] = "Branch:",
        ["BaseLabel"] = "Base:",
        ["AgentLabel"] = "Agent:",
        ["ReviewLangLabel"] = "Review nyelv:",
        ["DryRun"] = "Dry-run",
        ["InfoBtn"] = "Infó",
        ["SettingsBtn"] = "Beállítások",
        ["ExportMdTip"] = "Review exportálása Markdownba",
        ["ExportHtmlTip"] = "Review exportálása HTML-be",
        ["ThemeBtn"] = "Téma",
        ["UiLangToggle"] = "English",
        ["UpdateBtn"] = "Frissítés",
        ["UpdAvailableTip"] = "Elérhető frissítés: v{0} — kattints a telepítéshez",
        ["RunBtn"] = "Review indítása",
        ["StopBtn"] = "Stop",
        ["StUpdDownloading"] = "Frissítés letöltése… {0}%",
        ["StUpdRestarting"] = "Frissítés letöltve — újraindítás…",
        ["StUpdError"] = "Frissítési hiba: {0}",
        ["UpdDlgTitle"] = "Elérhető frissítés",
        ["UpdDlgBody"] = "Elérhető a(z) {0} verzió (jelenleg v{1}).\nLetöltsük és telepítsük most? Az alkalmazás újraindul.",
        ["UpdYes"] = "Frissítés és újraindítás",
        ["UpdNo"] = "Később",
        ["StepsHeader"] = "Lépések",
        ["TabReview"] = "Review",
        ["TabDiff"] = "Diff",
        ["TabHistory"] = "Előzmények",
        ["TabLog"] = "Napló",
        ["ReviewEmptyTitle"] = "Még nincs review",
        ["ReviewEmptyHint"] = "Indíts egy review-t, és itt megjelennek az AI észrevételei.",
        ["DiffEmptyTitle"] = "Még nincs diff",
        ["DiffEmptyHint"] = "Indíts egy review-t, és itt megjelenik a változás.",
        ["ShowAllFiles"] = "Mind",
        ["DiffResizeTip"] = "Húzd a fájl/kód oszlopok átméretezéséhez",
        ["HistoryHint"] = "Korábbi futások (kattints a visszatöltéshez)",
        ["HistRepoFilter"] = "Repó:",
        ["HistAllRepos"] = "Összes repó",
        ["HistUnknownRepo"] = "(ismeretlen)",
        ["StReady"] = "Készen áll.",
        ["StRunning"] = "Fut…",
        ["StDone"] = "Kész.",
        ["StCancelled"] = "Megszakítva.",
        ["StError"] = "Hiba: {0}",
        ["StNeedRepoBranch"] = "Adj meg egy repó útvonalat és egy branchet.",
        ["StSelectAgentLang"] = "Nincs .coderev.json ebben a repóban — válassz agentet és review nyelvet.",
        ["StNotRepo"] = "A kiválasztott mappa nem git repository.",
        ["StBranchesLoaded"] = "{0} helyi branch betöltve.",
        ["StConfigImported"] = "  (.coderev.json importálva)",
        ["StReviewOf"] = "Review: {0}  (base: {1})",
        ["MetaFormat"] = "{0} fájl · {1} hunk · prompt ~{2} B",
        ["StSummaryOut"] = "Kész — kiírva: {0}  ({1} ms)",
        ["StSummary"] = "Kész  ({0} ms)",
        ["StHistoryLoaded"] = "Előzmény betöltve: {0}",
        ["StNothingExport"] = "Nincs mit exportálni — előbb futtass egy review-t.",
        ["StExported"] = "Exportálva: {0}",
        ["Close"] = "Bezárás",
        ["AboutSubtitle"] = "AI-alapú Pull Request review — verzió {0}",
        ["AboutWhatHdr"] = "Mi ez?",
        ["AboutWhatBody"] = "Egy eszköz, amely egy git ág módosításait (diff) átadja egy AI-ügynöknek, és visszaadja a kód-review-t. A nehéz munkát (git, diff, prompt, az ügynök futtatása) a coderev motor végzi; ez a grafikus felület átláthatóan jeleníti meg a folyamatot, a diffet és a review-t.",
        ["AboutHowHdr"] = "Hogyan használd?",
        ["AboutHow1"] = "1.  Nyiss meg egy git repository-t a 📂 gombbal.",
        ["AboutHow2"] = "2.  Válassz branchet (a mező autocomplete-tel segít a helyi ágakból, de kézzel is beírható) és egy base referenciát (pl. origin/main).",
        ["AboutHow3"] = "3.  Válassz agentet és nyelvet, majd nyomd meg a ▶ Review indítása gombot.",
        ["AboutHow4"] = "A lépések, a diff és az AI válasza élőben jelennek meg. A review exportálható (.md / .html), a korábbi futások az Előzmények fülön visszatölthetők, a téma váltható.",
        ["AboutAiHdr"] = "Használat AI-jal",
        ["AboutAiBody"] = "Az AI-t egy külső parancssori „agent” adja, amelyet a motor meghív, és átadja neki a promptot (a diffet és a kontextust). Beépített agentek:",
        ["AboutAiCodex"] = "•  codex — OpenAI Codex CLI",
        ["AboutAiCopilot"] = "•  copilot — GitHub Copilot CLI",
        ["AboutAiClaude"] = "•  claude — Claude Code CLI",
        ["AboutAiCustom"] = "Egyedi ügynök is megadható a ⚙ Beállítások ablakban (agent-config JSON: parancs + mód).",
        ["AboutNeedHdr"] = "Mi szükséges hozzá?",
        ["AboutNeedGit"] = "•  git a PATH-on (a repó elemzéséhez).",
        ["AboutNeedAgent"] = "•  A kiválasztott agent CLI telepítve és bejelentkezve a PATH-on. Fontos: a Claude esetén a különálló Claude Code CLI kell (npm i -g @anthropic-ai/claude-code) — a szerkesztő-plugin önmagában nem elég.",
        ["AboutNeedEngine"] = "•  A coderev motor bináris: a CODEREV_BIN környezeti változó, vagy az alkalmazás mellé csomagolva, vagy a PATH-on.",
        ["AboutNeedTip"] = "Tipp: a Dry-run pipa bekapcsolásával AI (és költség) nélkül lefut a teljes folyamat — látod a lépéseket és a diffet, csak a review marad üres. Jó az induló kipróbáláshoz.",
        ["CfgHeader"] = ".coderev.json beállítások",
        ["CfgReload"] = "Újratöltés",
        ["CfgSave"] = "💾 Mentés",
        ["CfgBaseRef"] = "Base ref",
        ["CfgHeadRef"] = "Head ref",
        ["CfgLang"] = "Nyelv",
        ["CfgOut"] = "Kimeneti fájl (out)",
        ["CfgOutWatermark"] = "pl. review.md (üres = csak konzol)",
        ["CfgAgent"] = "Agent",
        ["CfgBuiltin"] = "Beépített:",
        ["CfgUseCustom"] = "Egyedi agent használata (felülírja a beépítettet)",
        ["CfgCustomHint"] = "Egyedi agent (JSON): pl. {\"name\":\"mycli\",\"cmd\":[\"mycli\",\"--in\",\"{prompt_file}\"],\"mode\":\"file\"}",
        ["CfgIncludeFull"] = "A módosult fájlok teljes tartalmát is beteszi (include-full-files)",
        ["CfgStrictFetch"] = "A fetch hibája végzetes (strict-fetch)",
        ["CfgNoProgress"] = "Spinner kikapcsolása (no-progress)",
        ["CfgContextLines"] = "Kontextus sorok",
        ["CfgAgentTimeout"] = "Agent timeout (mp)",
        ["CfgTemplate"] = "Sablon (template)",
        ["CfgTemplateWatermark"] = "pl. review_template.md",
        ["CfgObeyDoc"] = "Betartandó dokumentumok (obey-doc) — soronként egy",
        ["CfgStNoRepo"] = "Nincs megnyitott mappa/repository — a mentés nem elérhető. Nyiss meg egyet a főablakban (📂).",
        ["CfgStLoaded"] = "Betöltve: {0}",
        ["CfgStDefaults"] = "Nincs még .coderev.json — alapértelmezett értékek.",
        ["CfgStInvalidLang"] = "A nyelv csak hu vagy en lehet.",
        ["CfgStInvalidJson"] = "Az egyedi agent nem érvényes JSON.",
        ["CfgStSaved"] = "Mentve: {0}",
        ["CfgStSaveError"] = "Hiba a mentéskor: {0}",
    };
}

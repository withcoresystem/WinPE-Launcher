# WinPE Launcher — Engineering Notes

> Development log for the WinPE Launcher. The source lives at the **repository root**
> (flat layout: `src/`, `lib/`, `ref/`, `Launcher.csproj`, `build.cmd`, `apps-config.json`).
>
> Status: **validated** on real WinPE and repeatedly screenshot-verified on a Windows build
> VM. The Launcher is written in C# (WinForms / .NET Framework 4.5, x64) and packs down to a
> single ~4 MB exe with a RAM footprint under 30 MB.

Purpose: a compact WinForms + AntdUI launcher that runs inside WinPE, typically placed on
USB (`D:\Softwares\Launcher\`) or embedded into boot media. Originally built as the
front-end for the EasyDeploy deployment platform and later generalized into the
Universal Builder launcher.

---

## 1. Problems encountered & solutions

### 1.1 WinPE error: "side-by-side configuration is incorrect" (0x800736B1)

**Symptom:** copying `Launcher.exe` into real WinPE and running it failed with an SxS error.

**Root cause (two layers):**

1. **32-bit build in an image without WoW64.**
   - The default `Platform=AnyCPU` produced an I386 exe (COR flags `0x20003` =
     32BITREQUIRED + 32BITPREFERRED).
   - The WinPE image has **no WoW64** (`System32\wow64.dll` missing, `SysWOW64` stripped),
     so a 32-bit process cannot start → SxS error.
2. **`System32\comctl32.dll` is not present in the image** (it exists only under WinSxS).
   Without a manifest dependency the loader does not redirect to WinSxS → further failures.

**Fix (validated):**

- `Launcher.csproj`: `<PlatformTarget>x64</PlatformTarget>` → AMD64, PE32+, pure IL.
- Add `src/app.manifest` + `<ApplicationManifest>`:
  - dependency `Microsoft.Windows.Common-Controls v6.0.0.0 processorArchitecture=amd64`
    (redirects to WinSxS);
  - `supportedOS` (Win10/11 GUIDs) + `asInvoker`;
  - `dpiAware` handling (see §1.7).
- `AntdUI.dll` (net40, AnyCPU) loads fine in a 64-bit process; no rebuild needed.

**Rule for WinPE apps:** target **x64** (image has no WoW64) + a comctl32 v6 amd64 manifest.

### 1.2 AntdUI: empty combo, dropdown direction, localization

- Combo = `AntdUI.Select`; items = `AntdUI.SelectItem(text, tag)`.
- `Placement = AntdUI.TAlignFrom.TL` opens the dropdown **upwards** (the bar sits at the
  bottom of the screen).
- Use the `SelectedValueChanged` event; the value is the item **Tag** (not `SelectedIndex`).
- Empty-state key `NoData`: set `AntdUI.Localization.Provider` with a custom provider.
- Close the dropdown after selection via `Reset()` (`ExpandDrop=false`).
- Bar metrics: `BaseHeight=56`, `BaseFieldHeight=34`, `BasePad=16`.

### 1.3 Building without the .NET 4.5 targeting pack

The build VM has MSBuild but no .NET 4.5 targeting pack. Fix: vendor the reference
assemblies under `ref/.NETFramework/v4.5/` and point `TargetFrameworkRootPath` at them.
`build.cmd` finds MSBuild via `where` / `vswhere`.

### 1.4 Screenshot harness: a hidden console showed up in captures

A scheduled screenshot task opened its own console, which appeared in the capture and
covered the UI. Fix: every action task must use `-WindowStyle Hidden`. Run interactive
tasks over SSH via `Register-ScheduledTask` (Interactive logon, highest run level) and
`Start-ScheduledTask`. Note that `$env:USERDOMAIN` over SSH can report the wrong domain —
use an explicit `machine\user`.

### 1.5 DISM unmount syntax

`dism /image:<dir> /unmount /discard` → **Error 87**. Correct:
`dism /unmount-image /mountdir:<dir> /discard`. Mounting for offline inspection:
`dism /mount-image /imagefile:<wim> /index:1 /mountdir:<dir>`, and always unmount/discard
afterwards.

### 1.6 AntdUI modal / Select / build-lock pitfalls

- **`Select.MaxCount` defaults to 4** → the 5th item is cut with a scrollbar. Set
  `MaxCount = 8` (or higher).
- **Modal position:** `Modal.Config(new Target(Control anchor), ...)` with a control anchor
  masks only that control; `Target(Form)` centres on the whole bar. `TopMost` is inherited
  from the main form.
- **Modal does not dispose the form** after `ShowDialog` → the dialog class must
  `panel.Dispose()` in `finally` after `Modal.open()` returns.
- Dialog config pattern: `SetColorScheme(TAMode.Dark)`, `SetOk/SetCancel`, `SetMask`,
  `SetLoadingDisableCancel`, `SetDefaultAcceptButton`, `SetFont`. `OnOk` returning `false`
  keeps the dialog open.
- Shared static fonts must **not** be disposed.
- **ESC trap:** `MainForm.OnKeyDown` treats ESC as "close app" — never send ESC while no
  modal is open; close dialogs by clicking.
- Toast `Message.*` auto-closes after ~6 s by default (and was shortened to 3 s later).
- **Build lock:** `MSB3026` if `Launcher.exe` is running → `taskkill` first.
- **Threading:** heavy work on `AntdUI.ITask.Run`; UI updates marshalled back via
  `panel.BeginInvoke` with `IsDisposed/IsHandleCreated` guards.
- **Wi-Fi placeholder:** keep it generic ("Password"), never "WPA2".
- **Wi-Fi dialog layout:** two columns — left = scan/SSID list, right = manual entry.
- **Mouse context menu:** AntdUI `Input` hardcodes a Chinese context menu; disable it with
  `UseContextMenu = false` on all inputs/selects.

### 1.7 apps-config, DPI/brand, wpeutil guard, harness traps

- **`apps-config.json`** next to the exe: either `{ "apps": [ ... ] }` or a bare array;
  parsed by the hand-written `src/Services/AppConfig.cs` (the net45 reference pack has no
  `System.Runtime.Serialization`, so `DataContractJsonSerializer` is unavailable). A
  missing/invalid file yields an empty list → "No applications" toast.
- **Brand width** is measured (`TextRenderer.MeasureText`) so the title never clips.
- **`dpiAware=true`** was added to the manifest to fix icon/font scaling on 1024×768
  laptops.
- **wpeutil:** guard with `File.Exists` before confirming shutdown/reboot, showing
  "wpeutil is only available in Windows PE" on full Windows.
- **BitLocker key auto-format:** digits are grouped in sixes with `-` separators, capped at
  48 digits, caret moved to the end.
- **Wi-Fi:** manual scan only (no auto-scan on open).
- **Harness traps:** run tests through an interactive scheduled task (plain SSH gives blank
  captures); fetch PNGs one at a time; modal position drifts by ~10–20 px between runs, so
  re-measure from the capture instead of hardcoding; low resolutions may be rejected by the
  VM's `ChangeDisplaySettings` (verify on real hardware).

### 1.8 Fourth combo (Opened Windows), Applications from config

- **Opened Windows:** `EnumWindows` (z-order), filtered to visible, non-tool windows of
  other processes with a non-empty title; excludes shell classes. Selecting a row restores
  (`ShowWindow(9)`) and foregrounds the window. `MouseDown` refreshes the list and closes
  any other open dropdown (`CloseOthers`), otherwise two dropdowns stay open at once.
- **Applications** are **only** the entries declared in `apps-config.json` (no automatic
  file scanning). `AppScanner.Scan()` resolves relative paths across fixed drives under
  `\CORESYSTEM\Softwares`, `\CORESYSTEM\Scripts`, `\CORESYSTEM`; scripts (`.ps1/.bat/.cmd`)
  get a code icon. `.ps1` runs via `powershell -File`, `.bat/.cmd` via `cmd /c`, `.exe`
  directly.
- **Tools** gained Task Manager and System Info (5 items total).

### 1.9 Config entries lost, taskbar overlap, minimized windows, icon

- **Missing-comma tolerance:** the parser now accepts a missing `,` between object/array
  entries and skips a BOM (UTF-8-BOM files from PowerShell 5.1 / old Notepad previously
  parsed silently empty).
- **Config overwrite:** a `Rebuild` cleans `bin\Release` and re-copies the placeholder
  `apps-config.json`; always edit the **source** config and rebuild, then re-copy the keyed
  config if needed.
- **Taskbar overlap:** the shell taskbar can steal topmost after user interaction; the bar
  re-asserts topmost (`TopMost=false; true`) on load, activation, and before opening a
  dropdown.
- **Minimized windows** still appear in Opened Windows and can be restored+foregrounded.
- **Icon:** `<ApplicationIcon>src\icon.ico</ApplicationIcon>`.

### 1.10 Toast duration, timezone clock, BitLocker holder, default icon, single-exe

- **Toast 6 s → 3 s** across all calls.
- **Clock respects the configured timezone:** `NowClock()` computes from
  `GetTimeZoneInformation` every tick; a new `"timezone"` key applies via PowerShell
  `Set-TimeZone` at startup (WinPE has no `tzutil`).
- **BitLocker holder width** is measured from a 48-digit sample (plus padding) so the
  recovery key never clips.
- **Default app icon:** entries without an explicit icon get `AppstoreOutlined`
  (`CodeOutlined` for scripts).
- **Single-exe:** `lib\AntdUI.dll` is embedded (`<EmbeddedResource>` + `AssemblyResolve`),
  so the shipped output is just `{Launcher.exe, apps-config.json}`.

---

## 2. Project structure

```
.
├── build.cmd                # locate MSBuild + Rebuild Release
├── Launcher.csproj          # v4.5, PlatformTarget=x64, ApplicationManifest, TargetFrameworkRootPath
├── apps-config.json         # launcher config (branding, timezone, assistant, apps)
├── src/
│   ├── Program.cs           # entry point, exception handlers, localization, AssemblyResolve
│   ├── Theme.cs             # shared colour palette
│   ├── MainForm.cs          # the bar: 4 combos, icon actions, status, clock
│   ├── Services/            # Exec, Wi-Fi/BitLocker/TimeSync, AppConfig, AppScanner,
│   │                        # ChatEngine, MarkdownRtf, SystemDiagnostics, ScreenCapture, UiFonts
│   ├── Dialogs/             # DialogUi, WifiDialog, BitLockerDialog, Chat*, SystemDiagnosticDialog,
│   │                        # DialogDock, DialogBorder, ScrollHost
│   ├── Engine/              # chat-engine.ps1, diag-engine.ps1 (embedded at build time)
│   ├── Fonts/               # Inter (Regular/Bold), Hack mono
│   ├── Resources/           # assistant.svg, diagnostic.svg, screenshot.svg, network.svg
│   └── app.manifest         # comctl32 v6 amd64 + supportedOS + asInvoker + dpiAware
├── lib/AntdUI.dll           # AntdUI (build-time reference; embedded into the exe)
├── ref/.NETFramework/v4.5/  # vendored reference assemblies (no targeting pack needed)
├── ref/                     # screenshot.webp, builder.webp (documentation assets)
└── release/                 # prebuilt Launcher.exe + apps-config.json
```

---

## 3. Validated features

- **Bar:** full-width strip at the bottom of the screen (56 px, DPI-aware): whitebox title,
  four dropdowns (System, System Tools, Applications, Opened Windows), network status icon,
  two-line clock, and icon actions (Smart Assistant, System Diagnostic, Screenshot).
- **System:** Shutdown/Reboot (`wpeutil`, guarded), Wi-Fi, BitLocker unlock, Sync Time.
- **System Tools:** Command Prompt, Notepad, PowerShell, Task Manager, System Info.
- **Applications:** declared entries resolved under `\CORESYSTEM\{Softwares,Scripts}` on any
  fixed drive; `.ps1/.bat/.cmd/.exe` launch methods; correct default icons.
- **Opened Windows:** enumerate/activate/restore.
- **Smart Assistant:** chat with any OpenAI-compatible LLM; Markdown rendering; selectable
  and copyable replies; JSONL session history; ≤ 500-word replies (enforced by the engine);
  UTF-8 mojibake repair; explicit in-pane errors when the endpoint/key is missing.
- **System Diagnostic:** read-only overview via `Get-CimInstance` (System, CPU, RAM modules,
  storage health, volumes with BitLocker status, graphics, network, battery, problem
  devices); cached between openings; refreshes on demand.
- **Screenshot:** one-click or **PrintScreen** full-screen capture, saved to
  `USB:\Screenshots\IMG-<timestamp>.jpg`.
- **UI scale:** a single `uiScale` value in `apps-config.json` scales the bar and every
  dialog consistently, regardless of the machine's DPI.
- **Dialogs:** Wi-Fi, BitLocker, themed notifications.

**Not verifiable on the build VM** (needs real hardware): BitLocker key entry (no locked
volume), low-resolution/DPI laptops.

---

## 4. Build & deploy checklist

1. Stop any running `Launcher.exe` (avoids `MSB3026`), then run `build.cmd` → output
   `bin\Release\{Launcher.exe, apps-config.json}` (AntdUI is embedded in the exe).
   A rebuild overwrites `bin\Release\apps-config.json` with the placeholder — restore the
   keyed config if you need it (keys never live in source).
2. Quick platform check: `Launcher.exe` must be `machine=0x8664 (AMD64)`.
3. Copy `Launcher.exe` + `apps-config.json` to the target (e.g. `D:\Softwares\Launcher\`).
4. For UI changes, screenshot-verify on the build VM (hidden scheduled task).
5. Edit the source in this repository (the source of truth); keep no test artefacts on the
   build VM.
6. Clean up the VM after testing: unregister test tasks, unmount any image, remove stray
   scripts/captures.

---

## 5. Test environment

- **Build/test VM:** Windows with MSBuild, .NET SDK, and an interactive console session used
  for screenshot verification (screenshots run through a hidden scheduled task).
- **WinPE target:** real hardware or a VM booting the produced media (x64, no WoW64, .NET
  Framework and PowerShell optional components installed). Remoting is often unavailable, so
  diagnose by mounting the WIM offline.

---

## 6. Smart Assistant — implementation notes

### 6.1 Overview

- **Bar icon:** `src/Resources/assistant.svg` with two states (idle grey / active accent).
- **Chat modal** (`src/Dialogs/ChatDialog.cs`): a dark modal with a transcript, input box,
  and status line; follows the same lifecycle pattern as the other dialogs.
- **Engine** (`src/Engine/chat-engine.ps1`, PowerShell 5.1, TLS 1.2): reads the `assistant`
  section from `apps-config.json`, calls an OpenAI-compatible `/chat/completions`, appends
  to a JSONL session file in `%TEMP%`, and enforces the reply length.
- **Config path** is resolved from the executing assembly, not `AppDomain.BaseDirectory`
  (which points at `powershell.exe` when the engine is reflection-loaded).
- **BYOK:** endpoint/key are supplied by the user in `apps-config.json`; the repository copy
  keeps empty placeholders.

### 6.2 Test harness notes (VM automation)

- Use an interactive scheduled task as the runner; fetch captures one at a time.
- Prefer `PostMessage` (WM_LBUTTONDOWN/UP) over injected input, which is flaky on VMs.
- `PrintWindow` with `PW_RENDERFULLCONTENT` renders the AntdUI modal correctly — use it as
  the source of truth when a plain screen capture looks stale.
- For mock tests, run a local HTTP listener in a separate process.

### 6.3 Chat UI round (bubbles, avatars, dark scheme)

- Transcript uses `AntdUI.Chat.ChatList` with `TextChatItem` bubbles: right-aligned accent
  "You" bubbles with a drawn "U" avatar, left-aligned neutral "Agent" bubbles with the
  tinted assistant SVG. Multiline text is normalised before it reaches an item.
- History reload parses the JSONL session so reopening the modal restores the transcript.
- The list scrollbar is hidden via reflection (`ScrollBar.SIZE = 0`) while keeping wheel
  scrolling.
- The mask is kept **off** so the launcher bar keeps its colours; bar interaction is blocked
  by disabling controls instead of dimming the screen.

### 6.4 UX round: English-only, wheel scroll, bottom-right dock

- The system prompt forces **English-only** replies; fallback strings are English too.
- Wheel scrolling is forwarded to the transcript via an `IMessageFilter` because WinForms
  routes `WM_MOUSEWHEEL` to the focused control.
- The modal is docked to the **bottom-right** corner, above the bar (chatbox UX), and the
  transcript opens scrolled to the bottom.

### 6.5 Reply quality: mojibake, word-wrap, 500 words

- **Mojibake** (`â†’`): caused by `Invoke-RestMethod` in PS 5.1 decoding a charset-less
  response as cp1252. Fixed by reading the raw bytes and decoding as UTF-8 explicitly, plus
  a defensive round-trip repair.
- **Word wrapping:** AntdUI wraps per grapheme; the dialog pre-wraps at word boundaries
  (mirroring AntdUI's measurement) so words are never cut mid-word.
- **500 words** (not characters): enforced by the engine and reflected in the status line.
- Taller transcript and the assistant bubble name "Agent".

### 6.6 UI polish: dropdown hover, clock, combo arrow

- Dropdown hover highlight and the combo arrow colour are overridden via
  `AntdUI.Style.Set(..., "Select")`.
- Clock shows `h:mm tt` plus a `dd-MMM-yyyy` line, computed in the configured timezone.

---

## 7. Later rounds (Markdown, System Diagnostic, Screenshot, UI scale)

- **Markdown rendering:** assistant replies are rendered from a small Markdown subset
  (headings, bold/italic, inline and fenced code, lists, quotes, links, tables) into RTF
  displayed in a rich-text bubble. Code uses the embedded **Hack** monospace font. Replies
  are selectable and copyable, with a context menu (Copy / Select all / Copy entire
  conversation).
- **Custom scrollbar:** the chat transcript and the diagnostic text use a shared
  `ScrollHost` with a slim, auto-hiding scrollbar (wheel + draggable thumb).
- **System Diagnostic:** a read-only modal backed by `Get-CimInstance`; each section is
  guarded so a missing CIM class on WinPE never aborts the run; results are cached; volumes
  show a BitLocker badge; problem devices use friendly descriptions (e.g. "Driver not
  installed / not loaded (code 28)").
- **Screenshot:** full-screen capture saved to `USB:\Screenshots\` (media root detection),
  with a global **PrintScreen** hotkey so it works even while a dialog is open. The capture
  uses `BitBlt` with `CAPTUREBLT` so **layered console windows** (cmd/PowerShell) are
  included — plain screen capture omits them on systems without DWM composition.
- **Dialogs got a coloured border** (`DialogBorder`, a borderless overlay ring) so they stand
  out from other dark windows, and open directly at the bottom-right without an animation.
- **Single UI scale:** `apps-config.json`'s `uiScale` is applied to the bar and to every
  dialog by compensating for the system DPI, so the whole launcher shares one consistent
  scale; the default is 1 (100%).

# pixel-press — Plan

## Scope

A Windows 10/11 desktop utility for **batch image processing** via a stackable operation pipeline:

- **Resize** — by longest edge, exact WxH, percentage, or fit/fill target; optional upscale guard; high-quality resampling.
- **Convert** — between JPG, PNG, WebP, AVIF, BMP, GIF, TIFF (and decode HEIC where the platform codec is present).
- **Compress/optimize** — quality slider per format, target-file-size mode, optional metadata stripping.
- **Watermark** — text or image (logo) overlay with position, opacity, scaling.
- **Rename** — token-based output naming (`{name}`, `{index}`, `{width}`, `{date}`, `{ext}`) with sequential numbering.
- **Batch run** — apply the pipeline across many files/folders with a progress + result report (successes, skips, bytes saved).
- **Live preview** — before/after for the selected image and estimated output size.
- **Recipes** — save/load pipelines as JSON.

In scope: single-machine desktop use, common raster formats, deterministic offline processing.

## Architecture / tech approach

- **Language/runtime:** C# on **.NET 8**.
- **UI:** **WPF** (MVVM). File list with drag-drop, pipeline builder panel, preview pane, run dialog.
- **Core library (UI-free):** `PixelPress.Core`
  - `IImageOperation` pipeline (`Resize`, `Convert`, `Compress`, `Watermark`, `Rename/Output`), order-independent where sensible, applied in sequence.
  - `PipelineRunner` — enumerates inputs, applies operations, writes outputs, aggregates a `BatchResult` (per-file status, timings, size deltas). Parallelized with bounded concurrency + cancellation + progress reporting.
  - Imaging backend behind an interface (`IImageCodec` / `IImageProcessor`) so the concrete library is swappable. **Primary candidate:** [ImageSharp](https://github.com/SixLabors/ImageSharp) (cross-platform, WebP/PNG/JPG/GIF/TIFF, high-quality resamplers). AVIF/HEIC decode via platform codecs / optional native plugin behind the same interface.
  - Recipe (de)serialization to JSON.
- **Settings/recipes:** JSON under `%APPDATA%\pixel-press`.
- **Optional local-AI:** `IImageAiService` → Ollama / llama.cpp **OpenAI-compatible** endpoint (MiniCPM-V class for smart crop; small text model for naming). Reachability probe + graceful fallback; **off by default; local-only**.
- **Testing:** **xUnit** against `PixelPress.Core` (deterministic golden-image / dimension / format / size assertions).

### Solution layout (target)

```
pixel-press.sln
 ├─ src/PixelPress.Core/     # imaging pipeline, runner, recipes (no UI)
 ├─ src/PixelPress.App/      # WPF MVVM desktop app
 └─ tests/PixelPress.Core.Tests/  # xUnit
```

## Milestones

- **M0 — Bootstrap:** README, PLAN, issues. *(in progress)*
- **M1 — Core engine:** pipeline + resize/convert/compress operations + `PipelineRunner` + xUnit coverage.
- **M2 — WPF UI:** drag-drop input list, pipeline builder, live before/after preview, output-folder selection, run + result report.
- **M3 — Packaging:** portable self-contained **win-x64 zip** + **MSIX**; **CI on windows-latest** (build + test).
- **M4 — Optional local-AI:** smart crop + auto-naming behind opt-in provider with reachability probe/fallback.
- **M5 — Polish:** watermarking, target-file-size compression, headless CLI, recipe library.

## Non-goals

- Not a full raster/vector **editor** (no layers, brushes, retouching).
- No **cloud** upload, account, or SaaS backend — required core value is 100% offline.
- Not a **photo organizer / DAM** (that's `snapdex`); pixel-press only *transforms* images.
- Not a **duplicate finder / disk tool** (those are `dupe-sweeper` / `diskscape`).
- No raw-camera (`.CR2/.NEF`) *development* pipeline in v1.
- Cross-platform GUI is out of scope for v1 (Core stays portable; UI is Windows/WPF).

## Packaging target for Windows

- Portable **self-contained win-x64 zip** (no install; unzip & run).
- **MSIX** installer for Start-menu integration and updates.
- GitHub Actions CI on **windows-latest**: `dotnet build` + `dotnet test`; artifacts on tagged releases.

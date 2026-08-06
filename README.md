# pixel-press

**Windows batch image resizer, converter & optimizer.** Drop in a folder or a pile of images, apply a stack of operations — resize, convert format, compress, watermark, rename — and export them all in one pass, with a live before/after preview. Offline and privacy-first: every pixel is processed on your machine.

> Status: 🚧 Bootstrapping (docs + backlog). No app code yet — see [PLAN.md](PLAN.md) and the [issue backlog](https://github.com/rwrife/pixel-press/issues).

---

## Overview

pixel-press is a small, focused Windows 10/11 desktop utility for **bulk image processing**. Think of the classic "Image Resizer" PowerToy, but with a full operation pipeline: resize *and* convert *and* compress *and* watermark *and* rename in a single run, previewing the result before you commit.

It is built around a stackable, order-independent operation pipeline so you can save your favorite recipes (e.g. "web export: 1600px longest edge → WebP q80 → strip EXIF") and reuse them with one click.

## Motivation

- Preparing images for the web, email, or docs usually means opening a heavyweight editor and repeating the same steps dozens of times.
- Online converters/compressors require uploading private photos to a stranger's server.
- Existing tools each do *one* thing — you end up chaining a resizer, a converter, and a compressor.

pixel-press does the whole batch, locally, in seconds, with a preview so you know exactly what you'll get.

## Use cases

- **Web publishing:** shrink a folder of DSLR shots to 1600px WebP for a blog.
- **Email/upload limits:** compress a batch under a target file size.
- **Format migration:** convert HEIC/PNG libraries to JPG (or modern AVIF/WebP).
- **Branding:** stamp a logo/text watermark across product photos.
- **Tidy exports:** sequential rename with tokens (`vacation-001.webp`, `vacation-002.webp`).
- **Thumbnails:** generate fixed-size square crops for a catalog.

## How to use (Windows-first quickstart)

> Packaged builds land with Milestone 3. Until then, build from source (requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows 10/11):

```powershell
git clone https://github.com/rwrife/pixel-press.git
cd pixel-press
dotnet build
dotnet run --project src/PixelPress.App
```

Then:

1. **Add images** — drag-drop files/folders onto the window, or use *Add Files*.
2. **Build a pipeline** — add operations (Resize → Convert → Compress → Watermark → Rename). Reorder as needed.
3. **Preview** — select any image to see the before/after and the estimated output size.
4. **Choose an output folder** (or overwrite in place, with an optional backup).
5. **Run** — export the whole batch. A summary reports successes, skips, and space saved.

### Example workflow

Recipe: *"Web export"* — 1600px longest edge, WebP quality 80, strip metadata, keep aspect ratio.

```text
[Resize]   mode=LongestEdge  size=1600  upscale=false
[Convert]  format=WebP        quality=80
[Metadata] strip=true (keep orientation)
[Output]   folder=.\web  naming={name}-web.{ext}
```

Select all → **Run** → 240 photos exported to `.\web` in one pass.

### Command-line (headless) — planned

```powershell
pixelpress run --recipe "web-export.json" --in ".\raw" --out ".\web"
```

## Local-AI integration (optional)

pixel-press works fully **without any AI**. When you have a local model runtime available (e.g. [Ollama](https://ollama.com) or a llama.cpp server exposing an OpenAI-compatible endpoint), you can opt in to AI-assisted features:

- **Smart crop** — pick the most interesting region for square/thumbnail crops using a tiny local vision model (MiniCPM-V class).
- **Auto filenames / alt-text** — suggest descriptive names or captions for exported images.
- **Recipe suggestions** — recommend format/quality for a target use.

All AI is **local-only, off by default**, gated behind a reachability probe, and gracefully falls back to deterministic behavior (center crop, token-based naming) when no model is available. No image ever leaves your machine.

## Current status / milestones

- [ ] **M0 — Bootstrap:** README, PLAN, issue backlog *(in progress)*
- [ ] **M1 — Core engine:** `PixelPress.Core` pipeline (resize, convert, compress) + tests
- [ ] **M2 — WPF UI:** drag-drop list, pipeline builder, live preview, run/report
- [ ] **M3 — Packaging:** portable win-x64 zip + MSIX, CI on windows-latest
- [ ] **M4 — Optional local-AI:** smart crop / naming behind opt-in provider

## License

MIT (see repo).

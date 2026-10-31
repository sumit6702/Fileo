# Fileo

A high-performance Windows 11 desktop application for bulk image conversion across 9+ formats, featuring a native **Windows 11 Fluent Design** interface with Mica backdrop, Segoe Fluent Icons, in-window Fluent confirmation dialogs, dynamic light/dark theme matching, and an ultra-safe hybrid conversion engine.

---

## 🚀 Features

### 1. Modern Windows 11 Fluent Design Look
- **Mica Backdrop** & soft rounded corners conforming to Windows 11 design guidelines.
- **Fluent Modal Dialogs**: Pre-conversion confirmation dialog, cancellation prompts, and file error details are displayed using in-window Fluent overlays with badges, icons, and theme colors.
- **Adaptive Screen Sizing**: Automatically adapts window height and width on startup to fit any display, scaling factor (125%, 150%), or laptop resolution without extending off-screen.
- **Segoe Fluent Icons** throughout the interface.
- **Theme Matching**: Automatically adapts to Windows 11 system accent colors and dark/light mode, with manual toggle in the title bar.
- **Interactive Multi-Select Chips**: Filter input formats (`PNG`, `JPG`, `WebP`, `AVIF`, `HEIC`, `TIFF`, `BMP`, `GIF`, `All Supported`).

### 2. Flexible Output Location & Naming
- **Save in same folder as original**: Preserves existing file directories.
- **Save in separate output folder**: Mirrors original subfolder structure cleanly into a destination directory. Automatically validates and suggests destination paths.
- **Overwrite Policies**: Auto-rename (`image (1).ext`), Overwrite, or Skip.
- **Custom Prefix & Suffix**: e.g. `thumb_` or `_web`.

### 3. Hybrid Conversion Engine
- **Bundled `cwebp.exe` (libwebp v1.6.0 x64)**: Invoked directly via `Process` for native WebP conversions with granular control over quality, compression effort, near-lossless, sharp YUV, exact transparency colors, and thread counts.
- **Magick.NET (Q16-HDRI x64)**: Industrial-grade decoding of all input formats (including raw and container formats) and native encoding for:
  - **AVIF**: Quality 0-100, speed/effort 0-10, lossless toggle, chroma subsampling (4:2:0, 4:2:2, 4:4:4), bit depth (8, 10, 12-bit HDR).
  - **JPEG**: Quality 1-100, progressive scan, chroma subsampling, optimized Huffman tables, background color picker for transparency flattening.
  - **PNG**: Compression levels 0-9, Adam7 interlacing, optional 8-bit palette quantization (pngquant style).
  - **JPEG XL (JXL)**: Quality/distance slider, lossless toggle, compression effort (1-9).
  - **TIFF**: LZW, ZIP, JPEG, or uncompressed compression algorithms.
  - **BMP**: Flattening of transparency onto customizable canvas colors.
  - **GIF**: Color count (2-256), Floyd-Steinberg dithering, animated multi-frame support or first frame extraction.
  - **ICO**: Multi-size Windows icon generation packaging 16x16, 32x32, 48x48, 64x64, 128x128, and 256x256 layers.

### 4. Safety & Backup
- **Verified Backups**: Mirrors exact folder tree before conversion. Computes SHA-256 byte hashes to verify file integrity.
- **Strict Deletion Protection**: Original files are **never deleted** unless:
  1. The backup copy exists and is SHA-256 verified.
  2. The conversion succeeded without warnings/errors.
  3. The destination file exists and is greater than 0 bytes.
- **Space-Saving Guard**: "If output is larger than original, keep original" option discards bloated conversions.

### 5. Real-Time Progress & Windows Taskbar
- Real-time overall progress bar, elapsed time, ETA countdown, and conversion throughput (files/second).
- Live counter badges: **Converted** (green), **Skipped** (amber), **Failed** (red).
- Space savings indicator with green/amber badge for saved MB and percentage.
- Windows Taskbar progress integration (`TaskbarItemInfo`) displaying Normal, Paused, and Error states on the taskbar icon.
- Interactive Pause/Resume and Safe Cancellation.

### 6. Results & Auditing
- Virtualized DataGrid displaying per-file input/output formats, original vs new size, saved percentage, and duration.
- Double-click any row to view error details or diagnostics in a Fluent modal dialog.
- One-click **Export to CSV**.
- Native Windows toast notifications upon batch completion.
- Persistent file logging under `%AppData%\Fileo\logs\`.

---

## ⚡ Build & Run

### Prerequisites
- Windows 10 (19041+) or Windows 11
- .NET 8.0 SDK (or later)

### One-Command Build
Open a terminal in the project directory and run:

```powershell
dotnet build -c Release
```

### Run the App
```powershell
dotnet run -c Release
```

### Run Automated Tests
```powershell
dotnet test Tests/ImageBatchConverter.Tests.csproj
```

---

## 🔄 How to Update `cwebp.exe`

Fileo includes an official Google `cwebp.exe` (v1.6.0) inside the `tools/` folder. When a newer version of `libwebp` is released:

1. Download the latest official Windows x64 binary release from Google's repository:
   [https://storage.googleapis.com/downloads.webmproject.org/releases/webp/index.html](https://storage.googleapis.com/downloads.webmproject.org/releases/webp/index.html)
2. Extract `cwebp.exe` from the downloaded archive `libwebp-<version>-windows-x64.zip\bin\`.
3. Replace the file at:
   `tools/cwebp.exe`
4. Rebuild the project (`dotnet build`). The project automatically copies the updated `cwebp.exe` to the output build folder.

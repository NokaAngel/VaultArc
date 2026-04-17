# VaultArc

VaultArc is a native Windows archive manager built with **C#**, **.NET 8**, and **WinUI 3**.

## Why VaultArc
- Native Windows desktop UX (not web/Electron)
- Modern replacement path for traditional archive utilities
- Security-first extraction defaults
- Production-minded layered architecture

## MVP Features
- Open archives and browse contents without extraction
- Archive browser with folder navigation and double-click behavior
- Extract archives with path traversal protection
- Create ZIP archives with compression presets
- Job queue with progress and status history
- Archive integrity test queueing
- Recent archives storage
- Hash tools: MD5, SHA-256, SHA-512
- Hash comparison and CSV report export
- Dark mode settings
- Open Selected / Run Selected workflows from inside archives
- Managed temp extraction sessions for launch/open scenarios

## Format Support
- **Read/Extract**: ZIP, 7Z, TAR, GZ, XZ (`SharpCompress`)
- **Create**: ZIP, TAR, TAR.GZ/TGZ, TAR.XZ/TXZ, GZ, XZ, ARC (`System.IO.Compression` + `SharpCompress` + VaultArc native ARC)
- **RAR**: best-effort read/extract depending on runtime library support and archive variant

`.gz` and `.xz` creation currently uses TAR+compression semantics for multi-file/folder input (use `.tar.gz` / `.tar.xz` for clarity).

VaultArc does **not** claim RAR creation support in MVP.

## Solution Structure
- `VaultArc.App` - WinUI 3 UI, navigation, page flows, view models
- `VaultArc.Core` - contracts, result/error primitives
- `VaultArc.Models` - strongly-typed data models
- `VaultArc.Archive` - archive operations and integrity checks
- `VaultArc.Hashing` - hashing, compare, export
- `VaultArc.Security` - safe extraction policies
- `VaultArc.Jobs` - queue + progress + cancellation
- `VaultArc.Services` - app-level orchestration facade, JSON persistence, local logger
- `VaultArc.Tests` - unit tests

## Dependencies
- `Microsoft.WindowsAppSDK`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `SharpCompress`
- `xUnit`

## Build
1. Install Visual Studio 2022+ with:
   - .NET desktop development workload
   - Windows App SDK / WinUI tooling
2. Open `VaultArc.sln` (recommended for Visual Studio compatibility)
3. Restore and build:
   - `dotnet restore`
   - `dotnet build`

## Run
- Set `VaultArc.App` as startup project
- Launch via Visual Studio or `dotnet run --project VaultArc.App`

## Open / Run From Archive
- Double-click folder entries to navigate into that folder.
- Double-click file entries (or press Enter) to open selected entry behavior.
- `Open Selected` uses a managed temp session extraction and then opens with Windows defaults.
- `Run Selected` is enabled only for runnable file types (`.exe`, `.com`, `.bat`, `.cmd`, `.msi`, `.ps1`, `.jar`, `.lnk` with warning).
- Runnable/script launches show security warnings before execution.
- Launches use extracted temp paths with proper working directory.

## Temp Session Behavior
- Temp sessions live under `%LOCALAPPDATA%\VaultArc\Temp\Sessions\<session-id>\`.
- Session metadata tracks archive source and launched process ids.
- Old sessions are cleaned up on startup (default 24h policy) unless pinned/active.
- Sessions with running processes are not deleted automatically.
- UI includes action to open the last extracted temp session folder.

## Known MVP Limitations
- RAR creation is not supported (read/extract only when compatible)
- 7Z creation is not supported by the current writer backend (read/extract still supported)
- ARC archives require a password and currently use full-file rewrite for rename/delete/save-back operations
- Job queue is in-memory only (history persists only for active session)
- Explorer shell integration is planned, not yet implemented
- Built-in theme customization is stored per-user and applies at runtime (hex validation only; no advanced contrast analyzer yet)

## Packaging Notes
- WinUI project is MSIX-ready via Windows App SDK template setup
- Future packaging targets: signed MSIX + optional installer bootstrapper

## Security Notes
- Extraction paths are normalized and validated
- Entries resolving outside extraction root are blocked
- Sensitive system extraction locations are blocked by default
- Password values are not logged
- No silent autorun; users must explicitly trigger Open/Run actions
- Script and runnable entry launches show warning prompts

## Documentation
- Product specification: `PRODUCT_SPEC.md`
- Future feature map: `FUTURE_FEATURES.md`
- Roadmap: `VAULTARC_ROADMAP.md`

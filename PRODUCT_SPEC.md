# VaultArc Product Specification (MVP)

## Product Summary
VaultArc is a native Windows archive manager built with C#, .NET 8, and WinUI 3. The MVP focuses on practical archive workflows: open, inspect, extract, create ZIP archives, hash files, test integrity, and manage jobs with visible progress.

## Target Users
- Everyday Windows users handling downloaded archives
- Power users managing large archive batches
- Developers and IT technicians validating package integrity

## MVP Scope
- Open archive files and browse entries without extracting
- Extract archives with safe path validation and overwrite controls
- Create ZIP archives from files/folders
- Queue jobs with progress, elapsed time, and status
- Maintain recent archive history
- Integrity test mode for readable health checks
- Hash tools for MD5, SHA-256, and SHA-512
- Dark mode and responsive WinUI navigation shell

## Supported Formats (MVP)
- Read/extract: ZIP, 7Z, TAR, GZ, XZ (via SharpCompress)
- Create: ZIP only (via System.IO.Compression)
- RAR: read/extract only if SharpCompress supports the source archive variant at runtime

## Non-Goals (MVP)
- No custom `.varc` format yet
- No archive repair yet
- No shell extension installer integration yet
- No guaranteed encrypted ZIP creation in MVP

## Security Requirements
- Block path traversal (`zip-slip`) on extraction
- Validate entry paths against extraction root
- Prevent implicit extraction to sensitive system locations
- Never log plaintext passwords
- Fail safely on invalid or suspicious entry paths

## Architecture
- `VaultArc.App`: WinUI shell, navigation, MVVM, drag/drop-ready UI flows
- `VaultArc.Core`: interfaces, result/error contracts
- `VaultArc.Models`: typed models and records
- `VaultArc.Archive`: archive open/list/extract/create/integrity/preview
- `VaultArc.Hashing`: hash + compare + export
- `VaultArc.Security`: path validation and extraction safety policies
- `VaultArc.Jobs`: in-memory job queue, progress updates, cancellation
- `VaultArc.Services`: facade orchestrating application use-cases; local JSON settings/recent stores + local file logger
- `VaultArc.Tests`: unit tests for hashing and path safety

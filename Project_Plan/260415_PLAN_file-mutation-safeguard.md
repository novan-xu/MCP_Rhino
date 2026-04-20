# File Mutation Safeguard

## Background

The project already supports direct Rhino file mutation through object editing. Before any mutation happens, the workflow now needs a reusable preflight safeguard that can prevent editing open files and preserve rollback snapshots in an archive folder.

## Goal

Add a common preflight pipeline that runs before any file-changing operation and does the following:

- detect whether the target file is currently open or locked
- warn the user to close the file before mutation if needed
- create an archive snapshot in a sibling `archive/` directory
- name the snapshot with `YYMMDD_HHmm_FileName`
- keep all current-day backups, while retaining only the latest modified backup for older days
- reserve a future-ready archive agent skeleton for later browsing / restore / strategy expansion

## Architecture Mapping

- `Tools/`: expose readiness / archive maintenance tools
- `Skills/`: implement the fixed preflight workflow and archive-related subflows
- `Agents/`: add a thin file archive agent skeleton for future evolution
- `Application/`: define safeguard and archive service abstractions
- `Infrastructure/`: implement lock detection, snapshot creation, and retention cleanup
- `Contracts/`: define structured responses for readiness, snapshot, cleanup, and preflight

## Key Design Decisions

### 1. Skill First, Agent Skeleton Second

The actual behavior is a fixed workflow, so the real implementation lives in skills and services. A thin `FileArchiveAgent` is added only as a future-ready entry point for upcoming archive browsing and restore features.

### 2. Safeguard Integrated into File Mutation

The object editing apply flow calls the file mutation safeguard automatically, so all file-changing operations can share the same preflight logic.

### 3. Open State Detection Strategy

Readiness is determined by:

- presence of a `.rhl` lock marker as a signal
- an exclusive file access attempt as the decisive lock test

This avoids relying on Rhino process detection alone.

### 4. Archive Naming and Retention

Snapshots are stored in `archive/` beside the source file and use the format:

- `YYMMDD_HHmm_FileName`

Retention is scoped to the same original file name:

- keep all current-day backups
- for older days, keep only the last modified backup of that day

## Files

### Modified

- `src/MCP_Rhino.Server/Application/Interfaces/IFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectEditingService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/NoOpFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`

### Added

- `src/MCP_Rhino.Server/Contracts/Responses/FileMutationReadinessResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ArchiveSnapshotResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ArchiveCleanupResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/FileMutationPreflightResponse.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IFileOpenStateInspector.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IArchiveSnapshotService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IArchiveRetentionService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoFileOpenStateInspector.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/ArchiveSnapshotService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/ArchiveRetentionService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Skills/File/FileOpenStateCheckSkill.cs`
- `src/MCP_Rhino.Server/Skills/File/ArchiveSnapshotSkill.cs`
- `src/MCP_Rhino.Server/Skills/File/ArchiveRetentionSkill.cs`
- `src/MCP_Rhino.Server/Skills/File/FileMutationPreflightSkill.cs`
- `src/MCP_Rhino.Server/Agents/File/FileArchiveAgent.cs`
- `src/MCP_Rhino.Server/Tools/File/InspectFileMutationReadinessTool.cs`
- `src/MCP_Rhino.Server/Tools/File/CreateArchiveSnapshotTool.cs`
- `src/MCP_Rhino.Server/Tools/File/CleanupArchiveTool.cs`

## Usage

### Integrated Flow

Any file mutation that uses `IFileMutationSafeguard` will now run:

1. readiness inspection
2. archive snapshot creation
3. archive retention cleanup
4. actual file overwrite

### Manual Tools

- inspect readiness
- create snapshot
- cleanup archive

## Future Extensions

- archive browsing
- restore specific versions
- different archive policies by project or file type
- compression / migration decisions
- richer decision logic inside `FileArchiveAgent`
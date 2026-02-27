# ToH264GPU Migration Plan (Wrapper -> .NET Core)

## Goal

Move `ToH264GPU` business logic from PowerShell into C# core, while keeping PowerShell as thin wrapper.

Important:
- `ToH264GPU` keeps its own domain behavior.
- only overlapping/common logic is shared with `ToMkvGPU` core.

## Target shape

PowerShell:
- parameter parsing and pipeline handling only
- engine call only
- wrapper-level errors/output forwarding only

C#:
- all transcode decisions and command construction
- test ownership for functional behavior

## Slice 1: Core contracts for H264 flow

Deliverables:
- `H264TranscodeRequest` model
- `H264TranscodeEngine` entry point
- minimal probe->decision->command flow

Tests:
- basic orchestration tests
- no-op/soft output conventions

## Slice 2: Remux fast-path policy

Deliverables:
- remux eligibility policy:
  - mp4-family container check
  - codec checks
  - VFR suspicion check (`r_frame_rate != avg_frame_rate`)
  - guard flags (no denoise/fix-ts/downscale)

Tests:
- remux-only positive scenarios
- fallback to encode when any condition breaks

## Slice 3: Timestamp and audio policy

Deliverables:
- `FixTimestamps` manual + auto rules (`wmv/asf`)
- audio copy/transcode policy:
  - `aac/mp3` copy when allowed
  - forced AAC when required
  - AMR-NB special handling
  - bitrate corridor clamp

Tests:
- focused policy unit tests (one nuance per test)

## Slice 4: Rate control and downscale behavior

Deliverables:
- normal mode RC policy (adaptive `vbr` vs CQ fallback)
- downscale RC policy (caps/fallbacks)
- `KeepFps` and FPS cap rules for downscale
- reuse common downscale mechanics where valid

Tests:
- deterministic RC tests with fixed probe/file inputs
- edge cases for duration/bitrate parse/fallback

## Slice 5: H264 command builder

Deliverables:
- dedicated command builder for `ToH264GPU` behavior
- keep function-specific defaults (container, movflags, preset defaults)

Tests:
- command shape snapshots by scenario
- no command-logic in wrapper tests

## Slice 6: PowerShell wrapper switch

Deliverables:
- `ToH264GPU.ps1` reduced to thin wrapper over .NET engine
- probe seam compatibility where needed for wrapper tests only

Tests:
- replace functional Pester tests with wrapper contract tests:
  - pipeline input
  - parameter forwarding
  - output forwarding
  - wrapper error behavior

## Exit criteria

- Functional behavior tested in C# xUnit.
- PowerShell Pester tests are compact wrapper-only tests.
- No business branching remains in `ToH264GPU.ps1`.

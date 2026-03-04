# MediaTranscodeEngine

`.NET 9` engine and CLI for building media transcode commands.

## Architecture

- Core owns domain rules and decision logic.
- CLI is a thin wrapper (parse -> map -> call Core).
- Main extension axes are explicit in project structure:
  - `Codecs`
  - `Resolutions`
  - `Scenarios`
  - `Quality`
  - `Sampling`
  - `Classification`
  - `Compatibility`
  - `Profiles`

Key design points:

- Codec routing is polymorphic (`ITranscodeRoute` + selector).
- Execution is strategy-based (`ICodecExecutionStrategy`).
- Downscale support is data-driven via profile targets (`source -> target` policies).
- Sampling window policy is configurable from profile data.

## Repository Layout

- `src/MediaTranscodeEngine.Core` - domain, policy, command builders, adapters
- `src/MediaTranscodeEngine.Cli` - console host
- `src/MediaTranscodeEngine.Core/Profiles/ToMkvGPU.576.Profiles.yaml` - default profile data
- `tests/MediaTranscodeEngine.Core.Tests` - xUnit/NSubstitute/FluentAssertions
- `tests/MediaTranscodeEngine.Cli.Tests` - CLI contract/integration tests
- `MediaTranscodeEngine.sln` - solution

## Runtime Requirements

- `.NET SDK` `9.0.x`
- `ffprobe` `6.x+` with JSON output support
- `ffmpeg` `6.x+` with required filters/encoders (`h264_nvenc`, `scale_cuda`, etc.)

## Build And Test

```bash
dotnet restore
dotnet build
dotnet test
```

## CLI Usage

Show help:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --help
```

Generate command with scenario preset:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --scenario tomkvgpu
```

Preset with explicit override (`explicit > preset > defaults`):

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --scenario tomkvgpu --cq 21 --downscale 576
```

Generate info output for piped paths:

```bash
some_path_producer | dotnet run --project src/MediaTranscodeEngine.Cli -- --info --scenario tomkvgpu
```

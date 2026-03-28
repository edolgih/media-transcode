# Transcode

Transcode is a quality-first video processing CLI built around practical scenarios.

It helps choose predictable compression settings for each content type - films, cartoons, and anime - so visual quality is preserved where it matters without inflating file size unnecessarily.
A dedicated interpolation scenario improves motion smoothness and makes the result more visually pleasing while assembling the final GPU-accelerated H.264 output.
Instead of relying on one generic preset, the tool exposes explicit scenarios (`tomkvgpu`, `toh264gpu`, `toh264rife`) so each task follows a clear and repeatable processing path.

Technically, Transcode is a `.NET 9` runtime and CLI layer for media inspection and scenario-driven transcoding command generation.

Current runtime pipeline:

`argv -> CliArgumentParser -> CliParseResult(normalized ScenarioInput) -> CliTranscodeRequest(per input) -> VideoInspector -> SourceVideo -> TranscodeScenario -> ScenarioExecution`

The repository currently implements three application scenarios and produces per-file results:

- `tomkvgpu` - mkv-oriented GPU transcode/remux decisions
- `toh264gpu` - mp4-oriented H.264 GPU transcode/remux decisions
- `toh264rife` - H.264 interpolation decisions using the Dockerized `vsrife + TensorRT` backend
- normal mode: legacy-compatible command lines and `REM ...` diagnostics
- `--info` mode: short decision markers without an `ffmpeg` command

The CLI requires an explicit `--scenario <name>` argument. The current public scenarios are `tomkvgpu`, `toh264gpu`, and `toh264rife`.

## Scenario Intent

- `tomkvgpu` is the MKV-first compatibility path. It is aimed at appliance-style playback targets where a conservative MKV output and TV-friendly transcode/remux decisions are preferred.
- `toh264gpu` is the MP4/H.264-first path. It is aimed at general-purpose playback on full operating systems and web/mobile-friendly environments where H.264 in MP4 is the safer default.
- `toh264rife` is the interpolation path. It targets H.264 output with `x2` or `x3` frame-rate multiplication, uses the repository Docker image `media-transcode-rife-trt` as the interpolation backend, supports separate interpolation model quality profiles, and resolves the final NVENC encode from shared `content/quality profile` defaults.
- all three scenarios share the same inspection and profile-driven quality-first video-settings core (including source-video bitrate cap), but they intentionally make different container, remux, audio, and compatibility decisions.

## Repository Layout

- `src/Transcode.Core` - shared core model, input inspection, video settings, and base scenario contracts
- `src/Transcode.Cli.Core` - shared CLI parsing, request orchestration, and scenario registry contracts
- `src/Transcode.Cli` - console host and dependency wiring
- `src/Transcode.Scenarios.ToH264Gpu` - `toh264gpu` scenario runtime logic and CLI adapter
- `src/Transcode.Scenarios.ToH264Rife` - `toh264rife` scenario runtime logic and CLI adapter
- `src/Transcode.Scenarios.ToMkvGpu` - `tomkvgpu` scenario runtime logic and CLI adapter
- `tests/Transcode.Runtime.Tests` - shared core behavior unit tests
- `tests/Transcode.Cli.Tests` - CLI contract tests
- `Transcode.sln` - solution

## Requirements

- `.NET SDK` `9.0.x`
- `ffprobe` with JSON output
- `ffmpeg` with the required filters and encoders on the host
- `docker` with GPU access when using the `toh264rife` scenario
- a locally built `media-transcode-rife-trt` image from `tools/docker/rife-trt`

The CLI resolves tool paths from standard host configuration sources such as `appsettings.json` and environment variables. For `toh264rife`, only `Scenarios:ToH264Rife:DockerImage` is externally configurable. Cache volume names and the `docker` command name are built into the backend.

## Build And Test

```bash
dotnet restore
dotnet build Transcode.sln
dotnet test Transcode.sln
```

## Documentation

- [README.ru.md](README.ru.md) - Russian overview
- [docs/cli.md](docs/cli.md) - CLI usage and option reference
- [docs/cli.ru.md](docs/cli.ru.md) - Russian CLI usage and option reference
- [docs/architecture.md](docs/architecture.md) - architecture and timing/sync notes
- [docs/architecture.ru.md](docs/architecture.ru.md) - Russian architecture and timing/sync notes

# CLI Usage

## Show Help

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --help
```

## Generate Commands

Default command generation:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\\Src\\movie.mkv"
```

Info-only output:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\\Src\\movie.mkv" --info
```

`downscale 576`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\\Src\\movie.mkv" --downscale 576
```

Explicit `576` profile:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\\Src\\movie.mkv" --downscale 576 --content-profile film --quality-profile default
```

Overlay with explicit repair mode:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\\Src\\movie.mkv" --overlay-bg --sync-audio
```

Frame-rate cap:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\\Src\\movie.mkv" --max-fps 30
```

Read paths from stdin:

```powershell
Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | dotnet run --project src/MediaTranscodeEngine.Cli -- --info
```

## Supported Options

- `--help`, `-h`
- `--input <path>`; repeatable
- `--scenario tomkvgpu`
- `--info`
- `--keep-source`
- `--overlay-bg`
- `--downscale <int>`
- `--max-fps <50|40|30|24>`
- `--sync-audio`
- `--content-profile <anime|mult|film>`
- `--quality-profile <high|default|low>`
- `--no-autosample`
- `--autosample-mode <accurate|fast|hybrid>`
- `--downscale-algo <bilinear|bicubic|lanczos>`
- `--cq <int>`
- `--maxrate <number>`
- `--bufsize <number>`
- `--nvenc-preset <preset>`

## Requirements

- `.NET SDK` `9.0.x`
- `ffprobe` with JSON output
- `ffmpeg` with required filters and encoders such as `h264_nvenc` and `scale_cuda`

By default the CLI resolves binary paths from `src/MediaTranscodeEngine.Cli/appsettings.json`:

```json
{
  "RuntimeValues": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  }
}
```

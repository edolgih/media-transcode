# ToH264Rife Setup

This document describes the external stack required by the current `toh264rife` scenario.

## What The Scenario Needs

- `.NET SDK` `9.0.x`
- `ffprobe`
- `ffmpeg`
- `rife-ncnn-vulkan`
- a Vulkan-capable GPU with a working graphics driver

The current scenario is a command generator. It does not install or bundle the interpolation backend for you.

## Current Scenario Assumptions

The current `toh264rife` command renderer invokes the backend in this shape:

```text
"<RifeNcnnPath>" -i "<input_frames_dir>" -o "<output_frames_dir>" -n <target_frame_count> -m rife-v4 -f %%08d.png
```

Two practical implications follow from that:

- `Scenarios:ToH264Rife:RifeNcnnPath` must point to the `rife-ncnn-vulkan` executable.
- the current renderer targets the `rife-v4` model name explicitly

Keep the extracted backend package intact, including the bundled model directories that ship with the release package.

## Windows Setup

### 1. Download The Backend Package

Download the current Windows release archive of `rife-ncnn-vulkan` from the official releases page:

- <https://github.com/nihui/rife-ncnn-vulkan/releases>

The official project README states that the release package includes the binaries and required models.

### 2. Extract It To A Stable Tools Directory

Example PowerShell commands:

```powershell
New-Item -ItemType Directory -Force D:\Tools | Out-Null
Expand-Archive -Path C:\Downloads\rife-ncnn-vulkan-<windows-archive>.zip -DestinationPath D:\Tools\rife-ncnn-vulkan
Get-ChildItem D:\Tools\rife-ncnn-vulkan
```

After extraction, verify that the directory contains:

- `rife-ncnn-vulkan.exe`
- the bundled model folders, including `rife-v4`

### 3. Point The CLI To The Backend Executable

Option A: configure `appsettings.json`

```json
{
  "Tools": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  },
  "Scenarios": {
    "ToH264Rife": {
      "RifeNcnnPath": "D:\\Tools\\rife-ncnn-vulkan\\rife-ncnn-vulkan.exe"
    }
  }
}
```

Option B: set it through an environment variable for the current PowerShell session

```powershell
$env:Scenarios__ToH264Rife__RifeNcnnPath = 'D:\Tools\rife-ncnn-vulkan\rife-ncnn-vulkan.exe'
```

## Smoke Test

### 1. Verify The Backend Binary

```powershell
& 'D:\Tools\rife-ncnn-vulkan\rife-ncnn-vulkan.exe' -h
```

### 2. Verify CLI Wiring

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --info
```

### 3. Generate A Real Interpolation Command

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --target-fps 60 --keep-source
```

## Practical Note About The Model Path

The current scenario renders `-m rife-v4` as a relative model identifier rather than an absolute path.

In practice, keep the backend package and its bundled model folders together. If your local runtime does not resolve `rife-v4` correctly, run the generated command from the extracted backend directory or replace `-m rife-v4` in the generated command with an explicit model path.

This note is an inference from the current command shape in this repository, not a claim about every possible `rife-ncnn-vulkan` build.

## Troubleshooting

- If `rife-ncnn-vulkan.exe -h` fails immediately, first check the GPU driver and Vulkan availability.
- If the generated transcode command fails with a model-loading error, verify that the `rife-v4` model directory is present and reachable for the command you are running.
- If CLI startup fails before command generation, verify `Tools:FfprobePath`, `Tools:FfmpegPath`, and `Scenarios:ToH264Rife:RifeNcnnPath`.

## Official References

- Project README: <https://github.com/nihui/rife-ncnn-vulkan>
- Releases: <https://github.com/nihui/rife-ncnn-vulkan/releases>

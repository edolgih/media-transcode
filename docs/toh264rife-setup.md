# ToH264Rife Setup

This document describes the current external stack required by `toh264rife`.

## What The Scenario Needs

- `.NET SDK` `9.0.x`
- `ffprobe` on the host
- `docker`
- NVIDIA GPU access from Docker Desktop / WSL2
- a locally built `media-transcode-rife-trt` image

The current scenario is a command generator. It does not install the Docker image for you.

## Practical Model

`toh264rife` works like this:

- CLI runs on the host and uses host-side `ffprobe`
- the generated command starts a one-shot `docker run --rm`
- the directory that contains the processed files is bind-mounted into the container as `/workspace/work`
- input is read directly from that mounted directory
- output is written back into that same mounted directory
- TRT cache and source cache live in Docker named volumes and survive between runs

No source files are copied into the repository or into a separate host-side temp folder.

## Current Scenario Assumptions

The current `toh264rife` command renderer invokes the backend in this shape:

```text
docker run --rm --gpus all ^
  -v "<source_dir>:/workspace/work" ^
  -v <trt_cache_volume>:/workspace/cache/trt ^
  -v <source_cache_volume>:/workspace/cache/src ^
  media-transcode-rife-trt ^
  "/workspace/work/<input_file>" "/workspace/work/<output_file>" <fps_multiplier> <container> <interp_model> <cq> <maxrate_kbps> <bufsize_kbps>
```

Practical implications:

- `Scenarios:ToH264Rife:DockerImage` must point to a built local image name
- the backend uses built-in Docker named volumes for TensorRT and source caches
- Docker creates those cache volumes automatically on first use if they do not already exist
- the current image supports `vsrife` interpolation profiles `low`, `default`, and `high`
- current mapping is:
  - `low -> 4.25.lite`
  - `default -> 4.25`
  - `high -> 4.26.heavy`
- the required `vsrife` models are already bundled inside the image; no extra model download is needed for the current contract
- the container image bundles `ffmpeg`, but CLI planning still needs host-side `ffprobe`
- final `NVENC` settings are resolved from shared `content/quality profile` defaults without autosample in this scenario

## Build The Docker Image

From the repository root:

```powershell
docker build -t media-transcode-rife-trt -f tools/docker/rife-trt/Dockerfile tools/docker/rife-trt
```

The image is built on top of `styler00dollar/vsgan_tensorrt:latest_no_avx512` and bundles the thin runner used by `toh264rife`.

If you change anything under `tools/docker/rife-trt`, rebuild the image with the same command before generating a new `toh264rife` command.

Optional sanity checks:

```powershell
docker image ls media-transcode-rife-trt
docker run --rm --entrypoint ffmpeg media-transcode-rife-trt -version
```

`ffmpeg` is expected inside the image. `ffprobe` is not.

## Minimal Configuration

The repository default already points `toh264rife` at the local image and cache volume names:

```json
{
  "Tools": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  },
  "Scenarios": {
    "ToH264Rife": {
      "DockerImage": "media-transcode-rife-trt"
    }
  }
}
```

You can override the same values through environment variables:

```powershell
$env:Scenarios__ToH264Rife__DockerImage = 'media-transcode-rife-trt'
```

## Smoke Test

### 1. Verify Docker GPU Access

```powershell
docker run --rm --gpus all nvidia/cuda:13.0.1-base-ubuntu24.04 nvidia-smi
```

### 2. Verify CLI Wiring

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --info
```

This should print a short decision line that includes:

- source resolution and FPS
- `x2` or `x3`
- interpolation profile and model
- final encode profile and CQ/maxrate/bufsize

### 3. Generate A Real Interpolation Command

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --fps-multiplier 2 --keep-source
```

The generated command is expected to:

- use `docker run --rm`
- bind-mount the source directory into `/workspace/work`
- mount the Docker named volumes used for TRT cache and source cache
- print live `docker/vspipe/ffmpeg` output to the same console where the `.bat` file is executed

### 4. Generate With Explicit Interpolation Quality

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --interp-quality high --keep-source
```

### 5. Generate With Explicit Encode Profiles

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --content-profile anime --quality-profile high --keep-source
```

## Troubleshooting

- If `docker run --rm --gpus all ... nvidia-smi` fails, first check Docker Desktop GPU integration, the NVIDIA driver, and WSL2 GPU support.
- If CLI startup fails before command generation, verify `Tools:FfprobePath`, `Tools:FfmpegPath`, and `Scenarios:ToH264Rife:DockerImage`.
- If the generated command fails inside the container, rebuild `media-transcode-rife-trt` from `tools/docker/rife-trt`.

## References

- Docker backend base image: <https://github.com/styler00dollar/VSGAN-tensorrt-docker>
- Local runner image sources: [`tools/docker/rife-trt`](../tools/docker/rife-trt)

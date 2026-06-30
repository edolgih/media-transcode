# Backlog

## FFmpeg Hardware Decode Locality

Context:

- AV1 sources should keep NVENC encode, but avoid CUDA/NVDEC decode because AV1 NVDEC may be unavailable on some NVIDIA hardware.
- `tomkvgpu` now keeps this decision local to `ToMkvGpuFfmpegTool.ShouldUseHardwareDecode(...)`.
- `toh264gpu` currently computes `UseHardwareDecode` in `ToH264GpuScenario` because that flag already exists in the scenario decision execution payload.

Current test coverage:

- AV1 encode avoids `-hwaccel cuda` and adds `-pix_fmt yuv420p`.
- AV1 downscale uses CPU `scale=...:flags=...` before NVENC encode.
- Non-AV1 encode still uses CUDA decode and honors `nvdecMaxThreads`.
- `tomkvgpu` overlay covers both CPU overlay for AV1/no-downscale and CUDA overlay for non-AV1/downscale.

Future direction:

- If `toh264gpu` hardware-decode behavior changes again, consider moving the renderer-specific `UseHardwareDecode` decision into `ToH264GpuFfmpegTool`, closer to where `-hwaccel cuda`, `scale_cuda`, CPU `scale`, and `-pix_fmt yuv420p` are rendered.
- Keep `SourceVideoBitrateResolver` out of hardware decode and encoder decisions; it should remain limited to source bitrate estimation.

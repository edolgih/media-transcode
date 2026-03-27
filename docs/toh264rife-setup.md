# ToH264Rife Setup

Минимум, который нужен для `toh264rife`:

- `.NET SDK 9`
- `ffprobe` на host
- `docker`
- GPU доступен из Docker Desktop / WSL2

## 1. Собрать локальный image

Из корня репозитория:

```powershell
docker build -t media-transcode-rife-trt -f tools/docker/rife-trt/Dockerfile tools/docker/rife-trt
```

## 2. Проверить GPU в Docker

```powershell
docker run --rm --gpus all nvidia/cuda:13.0.1-base-ubuntu24.04 nvidia-smi
```

## 3. Проверить, что CLI видит сценарий

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --info
```

## 4. Сгенерировать рабочую команду

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --keep-source
```

## 5. Полезные варианты

`x3`:

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --fps-multiplier 3 --keep-source
```

Interpolation quality:

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --interp-quality high --keep-source
```

Encode profiles:

```powershell
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\Src\clip.mkv" --content-profile anime --quality-profile high --keep-source
```

## Notes

- Сценарий использует локальный Docker image `media-transcode-rife-trt`.
- Для каждого файла запускается отдельный `docker run --rm`.
- TRT cache и source cache живут в Docker named volumes и переиспользуются между запусками.
- Первый cold run может быть долгим, повторные warm run обычно заметно быстрее.
- После обновления Docker image первый запуск обычно снова компилирует TRT engines.
- При необходимости полного сброса кэшей: `docker volume rm media-transcode-rife-trt-cache media-transcode-rife-src-cache`.

# Использование CLI

English version: [cli.md](cli.md)

## Показать Справку

```bash
dotnet run --project src/Transcode.Cli -- --help
```

## Генерация Команд

Генерация команд для `tomkvgpu`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv"
```

Вывод только информации:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --info
```

`downscale 720`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 720
```

Явный профиль `576`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 576 --content-profile film --quality-profile default
```

`downscale 424`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 424
```

Overlay с явным repair mode:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --overlay-bg --sync-audio
```

Ограничение frame-rate:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --max-fps 30
```

Генерация команд для `toh264gpu`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.m4v"
```

`toh264gpu` downscale до `576`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --downscale 576
```

`toh264gpu` с явным выбором quality-oriented profile:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --content-profile film --quality-profile default
```

`toh264gpu` с явным audio-sync repair path:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --sync-audio --keep-source
```

Генерация команд для `toh264rife`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\\Src\\clip.mkv"
```

`toh264rife` с явным `x3` multiplier:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\\Src\\clip.mkv" --fps-multiplier 3 --keep-source
```

`toh264rife` с явным качеством interpolation model:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\\Src\\clip.mkv" --interp-quality high
```

`toh264rife` с явными encode-профилями:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\\Src\\clip.mkv" --content-profile anime --quality-profile high
```

`toh264rife` с явным выбором выходного контейнера:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264rife --input "D:\\Src\\clip.avi" --container mp4
```

Чтение путей из stdin:

```powershell
Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --info
```

## Поддерживаемые Опции

- `--help`, `-h`
- `--input <path>`; можно указывать несколько раз
- `--scenario <name>`; обязательно, сейчас поддерживаются `toh264gpu`, `toh264rife` и `tomkvgpu`
- `--info`

Опции `tomkvgpu`:

Параметры quality-oriented video settings:

- `--keep-source`; по умолчанию выключен
- `--overlay-bg`; по умолчанию выключен
- `--downscale <720|576|480|424>`; по умолчанию не применяется
- `--max-fps <50|40|30|24>`; по умолчанию без cap
- `--sync-audio`; по умолчанию выключен
- `--content-profile <anime|mult|film>`; по умолчанию `film`
- `--quality-profile <high|default|low>`; по умолчанию `default`
- `--downscale-algo <bilinear|bicubic|lanczos>`; по умолчанию profile default, сейчас во встроенных профилях это `bilinear`
- `--cq <int>`; по умолчанию resolved profile value
- `--maxrate <number>`; по умолчанию resolved profile value
- `--bufsize <number>`; по умолчанию resolved profile value
- `--nvenc-preset <preset>`; по умолчанию `p6`

Опции `toh264gpu`:

Параметры quality-oriented video settings:

- `--keep-source`; по умолчанию выключен
- `--downscale <720|576|480|424>`; по умолчанию не применяется
- `--keep-fps`; по умолчанию выключен
- `--content-profile <anime|mult|film>`; по умолчанию `film`
- `--quality-profile <high|default|low>`; по умолчанию `default`
- `--downscale-algo <bilinear|bicubic|lanczos>`; по умолчанию profile default, сейчас во встроенных профилях это `bilinear`
- `--cq <1..51>`; по умолчанию resolved profile value
- `--maxrate <number>`; по умолчанию resolved profile value
- `--bufsize <number>`; по умолчанию resolved profile value
- `--nvenc-preset <p1..p7>`; по умолчанию `p6`
- `--denoise`; по умолчанию выключен
- `--sync-audio`; по умолчанию выключен; при включении использует явный audio-sync repair path
- `--mkv`; по умолчанию выключен, поэтому выход остаётся MP4

Опции `toh264rife`:

- `--keep-source`; по умолчанию выключен
- `--fps-multiplier <2|3>`; по умолчанию `2`
- `--interp-quality <low|default|high>`; по умолчанию `default`
- `--content-profile <anime|mult|film>`; по умолчанию `film`
- `--quality-profile <high|default|low>`; по умолчанию `default`
- `--container <mp4|mkv>`; по умолчанию сохраняет source container, если это mp4 или mkv; иначе mp4

`toh264rife` отдельно резолвит качество interpolation model и отдельно финальный NVENC encode. По умолчанию interpolation идёт через средний путь (`default`), а финальный encode теперь использует общий profile resolver с оценкой source-video bitrate и затем применяет interpolation-specific uplift для maxrate и bufsize.

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output на host-стороне
- `ffmpeg` с нужными фильтрами и кодировщиками на host-стороне
- `docker` с доступом к GPU для `toh264rife`
- локально собранный image `media-transcode-rife-trt` из `tools/docker/rife-trt`

CLI получает пути к бинарникам из стандартных источников host configuration, например `appsettings.json` и переменных окружения. `toh264rife` использует Docker backend. Минимальный `appsettings.json` выглядит так:

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

Backend `toh264rife` использует встроенные Docker named volumes для TensorRT cache и source cache. Это не user-facing ключи конфигурации.

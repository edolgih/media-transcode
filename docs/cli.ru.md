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
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --content-profile film --quality-profile default --autosample-mode fast
```

`toh264gpu` с явным audio-sync repair path:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --sync-audio --keep-source
```

Чтение путей из stdin:

```powershell
Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --info
```

## Поддерживаемые Опции

- `--help`, `-h`
- `--input <path>`; можно указывать несколько раз
- `--scenario <name>`; обязательно, сейчас поддерживаются `tomkvgpu` и `toh264gpu`
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
- `--autosample-mode <accurate|fast|hybrid>`; по умолчанию `hybrid` и для encode, и для explicit downscale
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
- `--autosample-mode <accurate|fast|hybrid>`; по умолчанию `hybrid` и для encode, и для explicit downscale
- `--downscale-algo <bilinear|bicubic|lanczos>`; по умолчанию profile default, сейчас во встроенных профилях это `bilinear`
- `--cq <1..51>`; по умолчанию resolved profile value
- `--maxrate <number>`; по умолчанию resolved profile value
- `--bufsize <number>`; по умолчанию resolved profile value
- `--nvenc-preset <p1..p7>`; по умолчанию `p6`
- `--denoise`; по умолчанию выключен
- `--sync-audio`; по умолчанию выключен; при включении использует явный audio-sync repair path
- `--mkv`; по умолчанию выключен, поэтому выход остаётся MP4

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output
- `ffmpeg` с нужными фильтрами и кодировщиками, например `h264_nvenc` и `scale_cuda`

CLI получает пути к бинарникам из стандартных источников host configuration, например `appsettings.json` и переменных окружения. Минимальный `appsettings.json` выглядит так:

```json
{
  "RuntimeValues": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  }
}
```

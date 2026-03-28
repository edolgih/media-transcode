# Transcode

`Transcode` - CLI для quality-first обработки видео по прикладным сценариям.

Инструмент помогает подбирать предсказуемые параметры сжатия под тип контента - фильмы, мультфильмы и аниме - чтобы сохранять визуальное качество там, где это действительно важно, и не раздувать итоговый файл без необходимости.
Отдельный сценарий интерполяции повышает плавность движения и делает результат визуально приятнее, собирая финальное H.264-видео с GPU-ускорением.
Вместо одного универсального пресета здесь используются явные сценарии (`tomkvgpu`, `toh264gpu`, `toh264rife`), чтобы для каждой задачи был понятный и повторяемый путь обработки.

Технически `Transcode` - это `.NET 9` runtime и CLI-слой для инспекции медиафайлов и генерации сценарных команд транскодирования.

Текущая минимальная цепочка:

`argv -> CliArgumentParser -> CliParseResult(normalized ScenarioInput) -> CliTranscodeRequest(per input) -> VideoInspector -> SourceVideo -> TranscodeScenario -> ScenarioExecution`

Сейчас в репозитории реализованы три прикладных сценария, и CLI печатает per-file результат:

- `tomkvgpu` - mkv-ориентированные решения по GPU transcode/remux;
- `toh264gpu` - mp4-ориентированные решения по H.264 GPU transcode/remux;
- `toh264rife` - H.264 interpolation-сценарий с Docker backend-ом `vsrife + TensorRT`;
- в обычном режиме: legacy-compatible строки команд и `REM ...` диагностику;
- в `--info` режиме: короткие маркеры решений без `ffmpeg`-команды.

В CLI сценарий нужно указывать явно через `--scenario <name>`. Сейчас публично поддерживаются `tomkvgpu`, `toh264gpu` и `toh264rife`.

## Назначение Сценариев

- `tomkvgpu` - MKV-first compatibility path. Он ориентирован на более консервативный выход в MKV и решения по transcode/remux для appliance-style playback targets вроде телевизоров и похожих устройств.
- `toh264gpu` - MP4/H.264-first path. Он ориентирован на более общий playback на полноценной ОС и на web/mobile-friendly окружения, где H.264 в MP4 обычно является более безопасным default.
- `toh264rife` - interpolation path. Он ориентирован на выход в H.264 с умножением кадровой частоты `x2` или `x3`, использует repo-local Docker image `media-transcode-rife-trt` как backend интерполяции, поддерживает отдельные профили качества interpolation model и резолвит финальный NVENC encode из общих `content/quality profile` defaults.
- все три сценария используют общую inspection и profile-driven quality-first video-settings основу (с учетом source-video bitrate cap), но намеренно принимают разные container, remux, audio и compatibility решения.

## Структура Репозитория

- `src/Transcode.Core` - общая core-модель, инспекция входного файла, video settings и базовые контракты сценариев
- `src/Transcode.Cli.Core` - общий CLI parsing, orchestration request-ов и контракты registry/handler-ов сценариев
- `src/Transcode.Cli` - консольный host и dependency wiring
- `src/Transcode.Scenarios.ToH264Gpu` - runtime-логика и CLI adapter сценария `toh264gpu`
- `src/Transcode.Scenarios.ToH264Rife` - runtime-логика и CLI adapter сценария `toh264rife`
- `src/Transcode.Scenarios.ToMkvGpu` - runtime-логика и CLI adapter сценария `tomkvgpu`
- `tests/Transcode.Runtime.Tests` - unit-тесты общего core-поведения
- `tests/Transcode.Cli.Tests` - контрактные тесты CLI
- `Transcode.sln` - solution

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output
- `ffmpeg` с нужными фильтрами и кодировщиками на host-стороне
- `docker` с доступом к GPU при использовании сценария `toh264rife`
- локально собранный image `media-transcode-rife-trt` из `tools/docker/rife-trt`

CLI получает пути к tool-ам из стандартных источников конфигурации host-а, включая `appsettings.json` и переменные окружения. Для `toh264rife` снаружи настраивается только `Scenarios:ToH264Rife:DockerImage`. Имена cache volume и имя команды `docker` зашиты в backend.

## Сборка И Тесты

```bash
dotnet restore
dotnet build Transcode.sln
dotnet test Transcode.sln
```

## Документация

- [README.md](README.md) - English overview
- [docs/cli.md](docs/cli.md) - CLI usage and option reference
- [docs/cli.ru.md](docs/cli.ru.md) - использование CLI
- [docs/architecture.md](docs/architecture.md) - architecture and timing/sync notes
- [docs/architecture.ru.md](docs/architecture.ru.md) - архитектура и заметки по таймлайну/синхронизации

# Заметки По Архитектуре И Поведению

English version: [architecture.md](architecture.md)

Этот документ описывает текущее реализованное состояние архитектуры и runtime flow.
Если отдельный refactoring document фиксирует целевое упрощение, это не считается противоречием: `architecture*.md` остается описанием current state, пока код не изменен.

## Runtime Flow

Текущий runtime pipeline:

`argv -> CliArgumentParser -> CliParseResult(normalized ScenarioInput) -> CliTranscodeRequest(per input) -> VideoInspector -> SourceVideo -> TranscodeScenario -> ScenarioExecution`

Ключевые runtime-типы:

- `src/MediaTranscodeEngine.Cli.Core/Parsing/CliContracts.cs` - общий результат CLI parse, который несет нормализованный `ScenarioInput`.
- `src/MediaTranscodeEngine.Cli.Core/CliTranscodeRequest.cs` - per-input CLI request, построенный из общего parse result.
- `src/MediaTranscodeEngine.Runtime/Videos/SourceVideo.cs` - нормализованные факты о входном файле.
- `src/MediaTranscodeEngine.Runtime/Videos/VideoInspector.cs` - строит `SourceVideo` из probe-output.
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeScenario.cs` - контракт сценария, который отдает `FormatInfo(...)` и `BuildExecution(...)`.
- `src/MediaTranscodeEngine.Runtime/Scenarios/ScenarioExecution.cs` - итоговый scenario-level рецепт команд.
- `src/MediaTranscodeEngine.Runtime/Plans/VideoPlan.cs` и `src/MediaTranscodeEngine.Runtime/Plans/AudioPlan.cs` - shared stream-level примитивы, реально используемые несколькими сценариями.
- `src/MediaTranscodeEngine.Runtime/VideoSettings/*` - общий каталог и resolver video-settings, используемый несколькими сценариями.
- `src/MediaTranscodeEngine.Runtime/Tools/Ffmpeg/FfmpegExecutionLayout.cs` - общий helper для output/temp path layout и post-operations.
- `src/MediaTranscodeEngine.Scenarios.ToH264Gpu/Runtime/ToH264GpuDecision.cs` и `src/MediaTranscodeEngine.Scenarios.ToMkvGpu/Runtime/ToMkvGpuDecision.cs` - scenario-local rich model решений; это внутренние типы scenario projects, а не shared runtime contracts.

CLI wiring:

- `src/MediaTranscodeEngine.Cli/Program.cs`
- `src/MediaTranscodeEngine.Cli.Core/Parsing/CliArgumentParser.cs`
- `src/MediaTranscodeEngine.Cli.Core/Parsing/CliContracts.cs`
- `src/MediaTranscodeEngine.Cli.Core/Scenarios/CliScenarioRegistry.cs`
- `src/MediaTranscodeEngine.Cli.Core/Scenarios/ICliScenarioHandler.cs`
- `src/MediaTranscodeEngine.Scenarios.ToMkvGpu/Cli/ToMkvGpuCliScenarioHandler.cs`
- `src/MediaTranscodeEngine.Scenarios.ToMkvGpu/Cli/ToMkvGpuCliRequestParser.cs`
- `src/MediaTranscodeEngine.Scenarios.ToH264Gpu/Cli/ToH264GpuCliScenarioHandler.cs`
- `src/MediaTranscodeEngine.Scenarios.ToH264Gpu/Cli/ToH264GpuCliRequestParser.cs`
- `src/MediaTranscodeEngine.Runtime/Inspection/FfprobeVideoProbe.cs`
- `src/MediaTranscodeEngine.Scenarios.ToH264Gpu/Runtime/ToH264GpuScenario.cs`
- `src/MediaTranscodeEngine.Scenarios.ToH264Gpu/Runtime/ToH264GpuFfmpegTool.cs`
- `src/MediaTranscodeEngine.Scenarios.ToMkvGpu/Runtime/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Scenarios.ToMkvGpu/Runtime/ToMkvGpuFfmpegTool.cs`

Высокоуровневый CLI flow:

- общий CLI-слой парсит shared-аргументы, такие как обязательный scenario name, input paths и `--info`;
- выбранный scenario handler парсит scenario-specific argv ровно один раз в CLI-слое и возвращает нормализованный runtime request как `ScenarioInput`;
- общий CLI-слой сохраняет этот нормализованный scenario input в `CliParseResult`, а затем строит по одному `CliTranscodeRequest` на каждый input file без повторного parse scenario argv;
- затем обработка загружает source facts, просит handler создать сценарий из уже готового parsed input и вызывает `scenario.FormatInfo(...)` или `scenario.BuildExecution(...)`;
- concrete ffmpeg command rendering теперь живет внутри scenario projects; shared runtime больше не резолвит tools и больше не проводит выполнение через общую `plan/spec/tool`-цепочку;
- на практике добавление нового application scenario должно в основном означать добавление нового CLI scenario handler плюс runtime request/scenario-local rendering types, которыми он пользуется;
- ordinary encode и downscale теперь разделяют одну profile-driven video-settings ось: output-height buckets, content/quality profiles, bucket bounds и autosample/bitrate-hint adjustment приходят из shared video-settings profile catalog, а не из scenario-local hardcoded fallback-ов;
- runtime request/value types больше не знают raw spellings вида `--option`; CLI-слой выступает transport adapter-ом, а runtime остаётся domain source of truth.

## Runtime-CLI Boundary

Текущие правила границы:

- `Runtime` владеет domain request objects, supported-value catalogs, normalization, validation и scenario invariants.
- `CLI` владеет raw option names, argv token reading, required-value checks, parse diagnostics и help rendering.
- `CLI` может содержать option-to-domain binding logic, потому что это transport-adapter knowledge.
- scenario-specific argv должен парситься один раз на CLI boundary в нормализованный scenario input.
- `CliParseResult` и per-input `CliTranscodeRequest` несут этот нормализованный scenario input дальше; они больше не являются raw-only carrier-ами.
- info path, normal execution path и failure path должны использовать уже доступный parsed scenario input или обходиться без повторного parse.
- `CLI` не должен держать свои собственные domain supported-value lists, если те же значения уже существуют в `Runtime`.
- `Runtime` не должен содержать `--option` literals, argv parsing или CLI help formatting concerns.

Практически это означает:

- scenario-local CLI parsers переводят argv в runtime request objects ровно один раз;
- `CliArgumentParser` сохраняет этот parsed scenario object в `CliParseResult`;
- per-input CLI processing переносит этот же объект через `CliTranscodeRequest`;
- runtime request/value types валидируют canonical domain values;
- CLI help должен форматировать supported values из runtime-owned catalogs, а не дублировать эти списки.

## Правила Runtime-Модели

- `null` используется только для реальной семантики, такой как `unknown`, `not applicable` или настоящий неуказанный override.
- `null` не должен подменять пустую коллекцию, default object или API convenience в runtime-модели.
- Nullable override-поля допустимы только пока модель выражает ещё неразрешённое request intent; после resolution downstream runtime contract должен становиться non-null.
- Один семантический факт должен храниться в одном месте.
- Defaults должны разрешаться на границе слоя, который ими владеет.
- Взаимоисключающие режимы по возможности должны выражаться типами, а не комбинациями флагов и nullable-полей.

## Timing, FPS And Sync Notes

- Если цель только адаптировать видео, сохранив source fps и source timeline, `-fps_mode:v cfr` и `-r` не должны добавляться по умолчанию.
- Если fps должен меняться, target fps должен быть явным, и для output path уместен CFR.
- `-fflags +genpts+igndts` не является общим desync fix. Он пересобирает timestamps и может неудачно интерпретировать пограничный source.
- `-avoid_negative_ts make_zero` только сдвигает timestamps в более чистый zero-based range. Сам по себе sync он не чинит.
- `-af "aresample=async=1:first_pts=0"` активно меняет audio timing. Его нужно считать repair logic, а не encode-step по умолчанию.
- `asetpts=N/SR/TB` только перенумеровывает audio PTS из sample order. Он не растягивает и не чинит audio относительно несвязанного video timeline.
- `-shortest` только подрезает явные хвосты. Он не исправляет desync в середине.
- Если тот же desync повторяется в том же месте даже после упрощения команды, первый подозреваемый - source или playback device.

Текущее намерение:

- ordinary downscale/encode path остаётся минимальным;
- fps-cap path добавляет только те framing controls, которые действительно нужны;
- `--sync-audio` остаётся явным repair mode.

## Reference Data

Стабильные legacy reference data остаются в:

- `docs/reference/legacy-fe62f0c/ToMkvGPU.576.Profiles.yaml`

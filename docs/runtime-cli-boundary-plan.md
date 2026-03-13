# Runtime-CLI Boundary Status

## Status

The boundary refactor is implemented.

Current baseline:

- `Runtime` no longer contains raw CLI option names such as `--downscale`;
- `Runtime` no longer parses scenario argv;
- `CLI` owns transport syntax, help rendering, and parse diagnostics;
- `Runtime` remains the single source of truth for domain values and invariants.

This is the agreed isolation level to keep going forward.

## Current Boundary

### Runtime owns

- domain request objects and scenario request constructors;
- canonical supported-value catalogs;
- normalization and validation of domain values;
- profile-derived supported values;
- domain exceptions and invariants.

`Runtime` is the single source of truth for:

- which values are allowed;
- how values are normalized;
- what those values mean inside scenario logic.

### CLI owns

- raw option names such as `--content-profile` and `--downscale`;
- argv token reading and required-value checks;
- unknown-option and missing-value diagnostics;
- help rendering syntax;
- binding parsed option values into runtime request objects.

`CLI` is the single source of truth for:

- how the command line is spelled;
- how scenario options are read from argv;
- how CLI-specific errors are rendered.

### Allowed duplication

The only duplication allowed in `CLI` is option-to-domain binding.

Allowed:

- `--nvenc-preset` maps to runtime NVENC preset validation;
- `--content-profile` maps to runtime content-profile validation;
- `--downscale-algo` maps to runtime downscale algorithm validation.

Not allowed:

- separate supported-value lists in both `CLI` and `Runtime`;
- separate domain validation rules in both layers;
- CLI help formatting concerns encoded into runtime APIs.

## Implemented Inventory

### Runtime domain/value layer

- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsRequest.cs`
  - validates content/quality profiles, autosample mode, CQ, maxrate, and bufsize;
  - derives supported content/quality profiles from the runtime profile catalog.
- `src/MediaTranscodeEngine.Runtime/VideoSettings/DownscaleRequest.cs`
  - validates target height;
  - validates downscale algorithm.
- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsProfiles.cs`
  - runtime source of truth for supported downscale heights;
  - runtime source of truth for supported content/quality profile values.
- `src/MediaTranscodeEngine.Runtime/Tools/Ffmpeg/NvencPresetOptions.cs`
  - runtime source of truth for supported NVENC presets.
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuRequest.cs`
  - scenario-specific runtime request with domain invariants only.
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuRequest.cs`
  - scenario-specific runtime request with domain invariants only.

### CLI transport/binding layer

- `src/MediaTranscodeEngine.Cli/Parsing/CliArgumentParser.cs`
  - parses shared CLI options only.
- `src/MediaTranscodeEngine.Cli/Parsing/CliOptionReader.cs`
  - shared argv token reader for scenario-local parsers.
- `src/MediaTranscodeEngine.Cli/Parsing/CliValueFormatter.cs`
  - formats runtime-owned supported values into CLI help and error text.
- `src/MediaTranscodeEngine.Cli/Scenarios/ToMkvGpu/ToMkvGpuCliRequestParser.cs`
  - owns `tomkvgpu` raw argv parsing and CLI-specific diagnostics.
- `src/MediaTranscodeEngine.Cli/Scenarios/ToH264Gpu/ToH264GpuCliRequestParser.cs`
  - owns `toh264gpu` raw argv parsing and CLI-specific diagnostics.
- `src/MediaTranscodeEngine.Cli/Scenarios/ToMkvGpu/ToMkvGpuCliScenarioHandler.cs`
  - binds parsed CLI request into runtime scenario construction.
- `src/MediaTranscodeEngine.Cli/Scenarios/ToH264Gpu/ToH264GpuCliScenarioHandler.cs`
  - binds parsed CLI request into runtime scenario construction.

## What Was Removed From Runtime

Removed from `Runtime` and should not be reintroduced:

- `--option` constants;
- scenario `TryParseArgs(...)` methods;
- argv-level diagnostics such as:
  - `Unknown option`
  - `Unexpected argument`
  - `requires a value`
  - type-parsing messages tied to CLI transport syntax
- CLI help formatting helpers such as `HelpDisplay`-style APIs.

## Current Test Split

### Runtime tests verify

- constructor and invariant enforcement;
- canonical normalization;
- catalog-derived supported values;
- scenario behavior built from typed runtime requests.

### CLI tests verify

- argv parsing;
- unknown option handling;
- missing value handling;
- exact CLI error text;
- help rendering;
- mapping from parsed CLI arguments into runtime request objects.

## Notes On Representation

### `nameof`

`nameof` is not used as the primary source for external CLI tokens or allowed value strings.

It remains acceptable only for:

- exception parameter names;
- internal implementation details where the external contract is not derived from a renameable symbol name.

### Attributes

Reflection/attribute-based binding is intentionally not used here.

Reason:

- it would add hidden control flow;
- it would widen the abstraction surface;
- it would solve a small duplication problem by introducing a larger meta-model than this project needs.

## Remaining Tradeoffs

The current implementation is complete for the agreed isolation level.

There are still a few acceptable tradeoffs:

- `TryValidate(...)` and `CreateScenario(...)` both parse scenario args through the same CLI-local parser.
  - this is redundant work, but it does not violate the boundary;
  - only optimize it if profiling or future changes justify carrying parsed state.
- some runtime classes still use internal display helpers for exception text.
  - this is acceptable because those strings are domain-facing constructor diagnostics, not CLI help formatting APIs.

## Rule Going Forward

When adding or changing scenario options:

1. add or update domain validation in `Runtime` only if the value is part of the domain model;
2. add or update raw option parsing in the scenario-local CLI parser;
3. format help in `CLI` from runtime-owned supported values;
4. do not put transport syntax back into `Runtime`.

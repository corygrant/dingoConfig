# Contributing to dingoConfig

Read the [shared contributing guide](https://github.com/corygrant/dingoElec/blob/main/dev/CONTRIBUTING.md) first. It covers the branch flow, PRs and changes that span repos. **Open PRs against `development`.**

## Build and run

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build dingoConfig.sln
dotnet run --project web/web.csproj
```

`./publish.sh` (or `publish.ps1`) builds self-contained zips for every platform. You only need this to check a packaging change.

## Layout

The code follows a clean-architecture split: `domain` (devices and the CAN protocol) ← `application` (services) ← `infrastructure` (adapters and the comms pipeline) ← `web` (Blazor UI). Keep each dependency pointing in that direction. `CLAUDE.md` has a more detailed map of the code.

## Testing

You don't need hardware for most UI changes. Pick the **Simulated** adapter, and use a CAN log to play back traffic. In your PR, say which adapter and device you tested with.

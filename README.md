# LivingMetalGhost

LivingMetalGhost is a Windows desktop mascot shell for LLM-assisted chat. This scaffold targets a WPF single-file deployment model and keeps provider, skill, and ghost composition replaceable.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK

## Run

```powershell
dotnet run --project .\src\LivingMetalGhost\LivingMetalGhost.csproj
```

## Publish

```powershell
.\publish.ps1
```

## Data Path

`%APPDATA%\LivingMetalGhost`

## Providers

- Mock
- OpenAI-Compatible
- Gemini
- OpenAI
- Ollama

## Notes

- API keys are intended to be stored via Windows DPAPI.
- This MVP scaffold wires Mock and OpenAI-compatible flows first.
- Sensitive actions are intentionally out of scope for the MVP.


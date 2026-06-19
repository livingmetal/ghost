# LivingMetalGhost

LivingMetalGhost is a Windows desktop LLM character assistant inspired by
Ukagaka/Nanika-style companions.

The product has three distinct modes:

- **Daily**: lightweight, character-flavored conversation.
- **Story**: a fictional visual-novel/ORPG-style partner experience.
- **Advanced**: a practical workbench for agent-assisted tasks.

The current priority is architectural cleanup that preserves behavior. Automatic
character generation, image-generation APIs, and a new sprite-parts composition
engine are not part of the current scope.

## Story Input

Story mode uses a small text grammar:

```text
plain text     -> spoken dialogue
**text**       -> visible action / narration
(text)         -> private inner thought
```

Single-asterisk text is not action syntax. See
[`docs/ROLEPLAY.md`](./docs/ROLEPLAY.md) for the complete Story-mode rules.

## Slash Agent

Daily mode normally behaves as ordinary LLM conversation. A single leading
slash asks the basic LLM to select one approved capability, loads current data,
and then lets the character explain the verified result.

```text
/날짜
/시간
/문지캠퍼스 점심 식사
/서울 날씨
/지역: 부산 날씨
/내일 제주 날씨
```

Inputs beginning with `//` remain ordinary text so comments and paths are not
captured. Initial live capabilities are Korea date/time, KAIST Munji campus
menus, regional current weather through Open-Meteo, and relative reminders.
Explicit current and tomorrow regional weather requests are parsed locally and
do not require the LLM to know the forecast. The LLM only turns the verified
API result into a character response when it is available.

## Image Input

Daily, Story, and Advanced input panels include an image attachment button.
Gemini and other image-capable OpenAI-compatible endpoints receive the selected
image as a multimodal `image_url` data block.

- Supported files: PNG, JPEG, WEBP, HEIC, HEIF
- Maximum file size: 10 MB
- One image can be attached to each turn.
- Image bytes are used only for the current API request. Conversation history
  and logs store the file name marker, not the Base64 data.
- CLI/local providers that do not advertise image input return a clear error
  instead of silently ignoring the image.

## Requirements

- Windows 10 or 11
- .NET 10 SDK
- A WPF-capable Windows environment

The application is commonly published for `win-x64` and `win-arm64`.

If `dotnet` is not available through `PATH`, set `LIVINGMETAL_DOTNET` to the
full executable path.

```powershell
$env:LIVINGMETAL_DOTNET = "D:\tools\dotnet\dotnet.exe"
```

## Run

```powershell
dotnet run --project .\src\LivingMetalGhost\LivingMetalGhost.csproj
```

## Verify

Run the repository verification script:

```powershell
.\scripts\verify.ps1
```

If the local PowerShell execution policy blocks direct script execution:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

Release verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

The script restores and builds the application and runs the test project when
present.

## Publish

```powershell
.\publish.ps1 -RuntimeIdentifier win-x64
.\publish.ps1 -RuntimeIdentifier win-arm64
```

`publish-all.ps1` publishes both runtime identifiers.

## Runtime Data

User configuration, logs, Story state, memories, reminders, and workspace data
are stored under:

```text
%APPDATA%\LivingMetalGhost
```

## Repository Guide

- [`AGENTS.md`](./AGENTS.md): canonical development and AI-agent instructions.
- [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md): current architecture and
  recommended module boundaries.
- [`docs/ROLEPLAY.md`](./docs/ROLEPLAY.md): Story mode syntax and state rules.
- [`docs/SPRITE_ARCHITECTURE.md`](./docs/SPRITE_ARCHITECTURE.md): current
  character presentation model and future rigging boundary.
- [`plans/README.md`](./plans/README.md): future proposals and roadmaps.

## Providers

- Mock
- Gemini
- OpenAI-compatible APIs
- Local and installed-app providers for Advanced mode

## Safety

- Daily and Story modes must not execute local commands.
- Workspace-changing actions belong only in Advanced mode and require approval.
- Do not commit API keys or secrets.
- Do not auto-start external coding agents, auto-apply patches, or auto-approve
  workspace changes.

The separation between casual conversation, fictional roleplay, and practical
work is a core product invariant.

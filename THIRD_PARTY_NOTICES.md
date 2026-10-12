# Third-party dependencies.

| Component | Pinned version | Purpose | License / upstream |
|---|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | WinUI 3 and Windows application runtime | MIT; https://github.com/microsoft/WindowsAppSDK |
| NAudio.Wasapi | 3.1.0 | Shared-mode Windows audio output | MIT; https://github.com/naudio/NAudio |
| NAudio.Core | Transitive dependency of NAudio.Wasapi 3.1.0 | Audio format and sample-provider contracts | MIT; https://github.com/naudio/NAudio |
| .NET 10 SDK/runtime | Stable 10.0 SDK selected through global.json | Application runtime and build tools | https://github.com/dotnet/core |

NuGet also restores Windows SDK projection/runtime/build dependencies. Review the restored dependency notices when preparing a redistribution. This document identifies direct choices; it is not a replacement for their license texts.

No dependency binaries are bundled in the source ZIP. NuGet restores them during the first build. The demo WAVs are included and were generated locally from `tools/Veda.Render/DemoScore.cs`.

Version and API references checked for this starter:

- https://www.nuget.org/packages/NAudio.Wasapi/3.1.0
- https://naudio.github.io/NAudio/api/NAudio.Wave.ISampleProvider.html
- https://naudio.github.io/NAudio/api/NAudio.Wave.WasapiPlayerBuilder.html
- https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1
- https://learn.microsoft.com/windows/apps/package-and-deploy/unpackage-winui-app
- https://learn.microsoft.com/dotnet/core/tools/global-json

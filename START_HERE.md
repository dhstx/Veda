# Start here.

## 1. Put the folder on your desktop.

Extract the ZIP and keep the `Veda` folder. It contains all source files, documentation, and the demo soundtrack. There is no embedded Git history and no executable installer.

If Windows extracts an additional outer folder, use the inner folder containing `README.md`, `global.json`, and `Veda.slnx`.

The source update has passed 33 portable checks and an additional check against the five uploaded Suno songs. Its full Windows UI build and device playback have not yet been tested; follow [docs/VERIFICATION.md](docs/VERIFICATION.md) during your first Windows run.

## 2. Prepare your Windows development tools.

Install the **.NET 10 SDK**, not just the runtime: https://dotnet.microsoft.com/download/dotnet/10.0

Install a current Visual Studio release with support for .NET 10 and **Windows application development / WinUI**. Include the Windows SDK and .NET desktop tools. Microsoft's setup instructions are here: https://learn.microsoft.com/windows/apps/get-started/start-here

Open the folder in File Explorer, right-click its empty area, and choose **Open in Terminal**. Confirm:

```powershell
dotnet --version
```

It should report `10.0.xxx`. The repo selects a stable .NET 10 SDK and allows later .NET 10 feature bands.

## 3. Check and run.

For everyday use, double-click **Run-Veda.bat** in the extracted `Veda` folder. It runs the same launch command below, automatically uses the correct folder, and keeps errors visible if startup fails. It checks for `dotnet`, but the .NET 10 SDK and WinUI development tools above still need to be installed. The first launch restores packages; later launches rebuild only what needs updating.

Keep the launcher in the source folder. To put it on your desktop, right-click it in File Explorer, choose **Show more options → Send to → Desktop (create shortcut)**. The command window stays open while Veda runs and closes when Veda exits successfully. Close the Veda app to end the session.

From the repository root:

```powershell
dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void
dotnet run --project src/Veda.App -c Debug -p:Platform=x64
```

The first launch may take time while NuGet restores the Windows UI and audio packages. After restoration and building, gameplay playback does not require an online service.

Alternatively, open `Veda.slnx` in Visual Studio, select `Veda.App` as the startup project, choose x64 and the **Unpackaged** launch profile, and run it. If your Visual Studio release cannot open `.slnx`, update Visual Studio or use the commands above.

## 4. Listen to the complete sequence.

Press **Play**, then try:

1. Calm space for at least 10 seconds.
2. Travel.
3. Uncertain.
4. Combat.
5. Escape.
6. Relief.
7. Calm space.

Expect some changes to wait for the next beat or bar. Combat starts its ramp immediately. Relief takes longer to settle. Fast button presses replace older pending requests.

You can run Star Citizen alongside it and turn down the game's original music. The number shortcuts belong to this app window; they are not global hotkeys inside the game.

## 5. Add Suno material after the demo works.

For the music manager and described custom situations, follow [docs/MUSIC_MANAGER.md](docs/MUSIC_MANAGER.md). For the listening test, follow [docs/FULL_SONG_TEST.md](docs/FULL_SONG_TEST.md) and choose **Load song library**. There is no need to extract stems for that mode.

For the original synchronized-stem mode, use [docs/SOUNDTRACK_AUTHORING.md](docs/SOUNDTRACK_AUTHORING.md).

## 6. Put it on GitHub when you are ready.

The supplied `.gitignore` excludes build outputs and common private audio and credential filenames. It cannot identify every private file: review the staged list below before committing. The bundled demo WAVs remain eligible for tracking.

Using GitHub Desktop is the simplest option: choose **File → Add local repository**, select this folder, and choose **Create a repository here** if it is not yet a Git repository. Create your initial commit, then use **Publish repository**. Select private visibility if you want to keep the source private.

Or initialize it in PowerShell:

```powershell
git init -b main
git add .
git status --short
git commit -m "Add manual adaptive soundtrack prototype"
```

Review the staged files before committing. If you created a Suno pack outside the supplied audio patterns, make sure it is excluded as intended. Create an empty repository on GitHub, then use the remote/push instructions GitHub displays. No GitHub account details or remote are preconfigured here.

After uploading, the included workflow checks the core and builds the Windows app. It does not release it.

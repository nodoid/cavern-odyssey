# Cavern Odyssey

A 256-room pixel-art cave adventure for desktop and mobile, built with MonoGame and .NET 10.

Kip the cave bird has to rebuild the Enertree before the cavern falls dark. Fly, spit,
flap and feed the dispensa-monstas to forge passes through the rune doors, collect the
24 Enertree fragments and get back to the altar.

## Features

- **256 hand-built-then-procedurally-assembled rooms** in a 16 × 16 grid, one continuous
  scrolling map of 512 × 288 tiles.
- **Puzzle economy**: 12 dispensa-monstas, 24 rune doors in six colours, 36 fruits.
  Feeding a monsta three fruits forges the pass that opens matching doors.
- **Three difficulties** (Easy, Medium, Hard) that change enemy counts and score
  multipliers (×1, ×5, ×10).
- **Local top-ten high scores** with six-character arcade name entry.
- **Three synthesised music tracks** and a full set of sound effects — every sound is
  generated in code, with no audio assets.
- **Rebindable desktop keys**, mouse-driven level editor, and an in-game minimap.
- **Mobile touch controls** with the same game on phone and tablet.
- No ads, no in-app purchases, no network access, no tracking.

## Controls

| Action | Desktop | Mobile |
| --- | --- | --- |
| Move | Arrow keys or Z / X | On-screen stick |
| Flap / jump | Up or M | FLAP |
| Spit | Space | SPIT |
| Use | E | USE |
| Map | N | — |
| Pause | Esc | PAUSE |

Desktop keys can be remapped from **Options → Controls**.

## Repository layout

```
src/SuperSqij.Shared/      Game code shared by every platform
src/SuperSqij.DesktopGL/   Windows/Linux/macOS head (MonoGame DesktopGL)
src/SuperSqij.WindowsDX/   Windows head (MonoGame WindowsDX)
src/SuperSqij.iOS/         iOS head
src/SuperSqij.Android/     Android head
```

The assembly file name, namespaces and level-file header still use the original working title
(`SuperSqij`, `SQIJLEVEL 2`) because the bundle identifiers and file format keep those
names; everything the player sees is branded Cavern Odyssey.

## Requirements

- .NET SDK 10.0
- MonoGame 3.8.6 (restored from NuGet)
- For iOS: Xcode 26 or later with the iOS workload
- For Android: Android SDK 36 with the Android workload, plus a JDK
- Python 3 with Pillow for the asset and screenshot tools

## Building and running

Desktop (macOS, Linux, Windows):

```bash
dotnet run --project src/SuperSqij.DesktopGL/SuperSqij.DesktopGL.csproj
```

Headless content check (verifies level generation, door/monsta balance, vault sealing and
that every difficulty produces a complete level):

```bash
dotnet run --project src/SuperSqij.DesktopGL/SuperSqij.DesktopGL.csproj -- --verify
```

iOS device build (Debug signs with `devel-supersqij`, Release with `rel-supersqij`):

```bash
dotnet build src/SuperSqij.iOS/SuperSqij.iOS.csproj -c Debug
dotnet publish src/SuperSqij.iOS/SuperSqij.iOS.csproj -c Release -f net10.0-ios \
  -r ios-arm64 -p:BuildIpa=true -p:IpaPackageDir=build/ios-release
```

Android package:

```bash
dotnet publish src/SuperSqij.Android/SuperSqij.Android.csproj -c Release \
  -f net10.0-android -p:AndroidPackageFormat=aab
```

## Licence

The **DILLIGAF Licence** — © 2026 Paul F. Johnson. See [LICENSE](LICENSE).

Do whatever you like with the source: use it, fork it, ship it, sell it. Attribution is
appreciated, not required, and there is no warranty of any kind. MonoGame and the other
NuGet dependencies keep their own licences.

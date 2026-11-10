# Bentley's Fast Food & Useful Stuff — One Mod, One DLL

A Stardew Valley 1.6 mod (SMAPI 4.x + Content Patcher) that adds **Bentley's Fast Food** restaurant to the Bus Stop, its food, Bentley the restaurant owner, a Restaurant Robot, a dessert corner and an arcade.

## What the mod does

- **Restaurant** on the Bus Stop, open 24 hours a day, with seating, a dessert corner (left room) and an arcade (right room, Journey of the Prairie King and Junimo Kart).
- **Ordering counter menu**: press the action button on the counter (tiles 14–18, row 5) and Bentley asks *"What would you like to see?"* — **Main Menu**, **Breakfast**, **Seasonal** or **Never mind**.
  - **Main Menu**: the 7 regular items (Cheeseburger, French Fries, Chicken Nuggets, Coke, Milkshake, Windex, Farmhand Snack Pack).
  - **Breakfast**: the 5 breakfast items. Outside 6–10 AM the counter says *"Breakfast is served from 6 AM to 10 AM."*
  - **Seasonal**: only the current season's items (2 per season, 8 in total).
- **Restaurant Kiosk**: a craftable, placeable kiosk (recipe at 2 hearts with Bentley). Choose Desserts, Food or Seasonal; 3 purchased units a day are 10% off.
- **Windex** hurts you when you drink it (configurable), **doors** (restaurant and Bentley's bedroom), a one-time **update announcement**, and the **Bus Stop guard**, which keeps the restaurant in place and clears trees and bushes from the lawn.

## Fast Food and BusStopGuard are integrated

This repository used to be three parts: the Fast Food code DLL, a separate **BusStopGuard** DLL and a separate **Kiosk** DLL. They are now **one C# project, one `ModEntry`, one DLL**:

```
src/
  BentleyFastFood.csproj          one project -> BentleyPlayz.FastFoodUsefulStuff.Smapi.dll
  ModEntry.cs                     the only SMAPI entry point; owns every event and console command
  Shared/                         ModContext (IDs, helpers), ModConfig, DevLog
  FastFood/                       DoorController, DoorRules, WindexDrinkController, CounterMenuService
  Kiosk/                          KioskService   (was the Kiosk DLL)
  BusStopGuard/                   BusStopGuardService, BusStopGuardConfig   (was the BusStopGuard DLL)
  Announcement/                   AnnouncementContent, AnnouncementMenu
Code/                             SMAPI part of the mod: manifest.json, config.json, announcement.json (+ the DLL after the build)
Content/                          Content Patcher part: manifest.json, content.json, data/, assets/
```

The mod folder still has **two manifests**. That is a SMAPI rule, not a leftover: a Content Patcher pack (`ContentPackFor`) and a code mod (`EntryDll`) cannot share one `manifest.json`. There is only **one DLL** and only one manifest that points at it (`Code/manifest.json`). UniqueIDs are unchanged, so saves keep working.

## Settings (outside the DLL)

Everything is in **`Code/config.json`**, created/updated automatically the first time the game starts. No recompiling needed.

| Setting | Default | What it does |
|---|---|---|
| `WindexDrinkDamage` | 10 | Health lost drinking Windex (0 = none) |
| `WindexSwirlConeDamage` | 10 | Health lost eating the Windex Swirl Cone (0 = none) |
| `BreakfastStartTime` | 600 | Breakfast starts (game time, 600 = 6:00 AM) |
| `BreakfastEndTime` | 1000 | Breakfast stops (1000 = 10:00 AM) |
| `KioskDealsPerDay` | 3 | Discounted units per day |
| `KioskDiscountPercent` | 10 | Kiosk discount |
| `DevLogging` | true | Extra `[DEV]` log lines (see `DEV_LOGGING.md`) |
| `BusStopGuard` | section | `Enabled`, `ClearOncePerDay`, `CheckIntervalTicks`, `MaxRecoveryAttemptsPerDay`, `VerboseLogging` |

If you had a `BusStopGuard.config.json`, its values are copied into the `BusStopGuard` section once. In game, `bentley_reload_config` in the SMAPI console re-reads the file.

Prices, items and the seasonal list are in `Content/data/*.json` (`shop.json`, `breakfast.json`, `seasonal.json`) and can be edited the same way.

Console commands: `bentley_announcement show|reset`, `bentley_windex_test [cone]`, `bentley_reload_config`.

## Build locally

Needs the .NET 6 SDK and a Stardew Valley install with SMAPI.

```bash
./build.sh                          # finds the game automatically
./build.sh "/path/to/Stardew Valley"   # or give the game folder
```

or just `dotnet build src/BentleyFastFood.csproj -c Release`. The DLL is written to `src/bin/Release/net6.0/BentleyPlayz.FastFoodUsefulStuff.Smapi.dll`. `build.sh` also assembles `release/BentleysFastFood.zip`.

## How GitHub Actions builds the mod

`.github/workflows/build.yml` runs on every push and pull request to `main`, and by hand (**Actions → Build Bentley's Fast Food → Run workflow**). It: checks out the code, installs .NET 6, sets up the SMAPI build environment, restores and builds `src/BentleyFastFood.csproj`, verifies the DLL and that `Code/manifest.json` names it, validates all JSON, assembles the mod folder, fails unless the package has **exactly one DLL**, zips it and uploads the ZIP.

The build needs the game's reference DLLs. The workflow tries `Pathoschild/SMAPI-ModBuildWorkflow/add-build-environment@v1`; if you cannot use that, copy the game's DLLs into a `lib/` folder in a **private** copy of the repository and the workflow uses them (see the comments in the workflow).

## Download the compiled ZIP

1. Open the repository on GitHub → **Actions** tab.
2. Click the latest successful **Build Bentley's Fast Food** run.
3. Under **Artifacts**, download **BentleysFastFood** (a ZIP that contains `BentleysFastFood.zip`).

## Install

1. Delete the old folders: `BentleyPlayz FastFoodUsefulStuff`, `BentleyPlayz_FastFoodUsefulStuff`, `BentleyPlayz_FastFoodUsefulStuff_SMAPI`, and any old **Kiosk** folder.
2. Unzip so that `BentleysFastFood` sits directly inside the game's `Mods` folder:

```
Mods/BentleysFastFood/
  Code/       manifest.json, config.json, announcement.json, BentleyPlayz.FastFoodUsefulStuff.Smapi.dll
  Content/    manifest.json, content.json, data/, assets/
```

3. Requirements: Stardew Valley 1.6.15, SMAPI 4.1.10+, Content Patcher 2.6.0+.

Copy your old `config.json` into the new `Code` folder if you changed any setting.

See `Content/CHANGELOG.md` for the version history. **Version 3.16.2 has not been built or tested in game yet.**

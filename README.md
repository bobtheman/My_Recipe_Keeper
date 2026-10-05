# My Recipe Keeper

Android (.NET MAUI Blazor Hybrid) cookbook: import recipes from links, edit them, send a shopping list or full recipe to WhatsApp / Notes.

## Projects
| Project | What |
|---|---|
| `My_Recipe_Keeper.Core` | Plain .NET 10 — SQLite repository, recipe scraper, share formatter, Google Drive backup. No Maui refs, so `dotnet test` runs without mobile workloads. |
| `My_Recipe_Keeper` | Maui head (Android only) — Blazor UI, share-intent target, platform adapters. |
| `My_Recipe_Keeper_Unit_Test` | xUnit + NSubstitute tests for Core. |

## Recipe import (free, no API keys)
1. `schema.org/Recipe` JSON-LD — nearly every recipe site/blog has it.
2. TikTok — public oEmbed endpoint returns the caption.
3. Instagram / Facebook / Threads — caption from `og:description` (fetched with a link-preview user agent), parsed by `RecipeTextParser`.
4. Anything else — title/photo/description from meta tags; user fills the rest.

Share a link from any app → pick **My Recipe Keeper** → it opens the importer automatically.

## Versioning
`ApplicationDisplayVersion` / `ApplicationVersion` in `My_Recipe_Keeper.csproj`.
Release Android package builds run `Scripts/RenameAPK.ps1` (→ `My_Recipe_Keeper_<display>_<build>.apk/.aab`) then `Scripts/IncrementVersion.ps1` (bumps `ApplicationVersion` for next release).

## Google Drive backup
`appsettings.json` ships placeholder `Google:ClientId` / `ClientSecret` — supply real values per build, never commit them.
Uses the `drive.appdata` scope and a loopback redirect on port 12347.

## Test
```
dotnet test My_Recipe_Keeper_Unit_Test
```

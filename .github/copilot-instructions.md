## Quick context

- This is a small Windows Forms utility (WinForms) that duplicates Smartpage dynamic timetables by calling the Smartpage HTTP backend.
- UI + HTTP logic live in `MainForm.cs`. Data models are in `Models/TimetableItem.cs`.

## Big picture

- Main components:
  - `MainForm.cs` — UI and the application's HTTP client logic. Contains server URL map (`_serverUrls`), load/save flows and status UI.
  - `Models/TimetableItem.cs` — JSON-deserialized model (includes `DynamicRow` / `DynamicCell`).
  - Project file `DuplicateTT.csproj` — targets `net10.0-windows` and enables WinForms.

- Data flow summary: user enters an ID → `load-brief` and `load` endpoints are called (GET). The response is deserialized into `TimetableItem`. On save, a small JSON (name, width, height, groupIds) is POSTed to `save-brief`.

## Important implementation patterns and conventions

- HTTP headers: every request sets an Authorization header with a bearer token and a `sessionid` header. See `btnLoad_Click` and `btnSave_Click` in `MainForm.cs` for exact usage.
- Serialization: uses `System.Text.Json` with `PropertyNameCaseInsensitive = true` for deserializing backend responses and `JsonNamingPolicy.CamelCase` when composing the minimal save payload.
- Minimal save payload example (constructed in `btnSave_Click`):

```json
{
  "name": "New name",
  "width": 1920,
  "height": 1080,
  "groupIds": [1,2]
}
```

- Server endpoints are defined in `_serverUrls` (DEV/DEMO/PROD). To change endpoints, edit `_serverUrls` in `MainForm.cs`.
- UI conventions: status text is updated via `SetStatus(string, Color)` which sets both message and color. Async event handlers are `async void` (typical for WinForms event handlers).

## Build / publish / run workflows

- Build with dotnet (PowerShell):

```powershell
dotnet build "c:\Users\perczelg\Documents\_Munka_\Hermész\-=PeGe tools=-\SmartpageDuplicates\DuplicateTT\DuplicateTT.csproj"
```

- A VS Code task exists in the workspace for building and for publishing a self-contained exe. In this repo the `csproj` is `DuplicateTT.csproj` and target framework is `net10.0-windows`.
- Publish self-contained exe (release, win-x64) — this matches the existing task that produces a single-file exe:

```powershell
dotnet publish "DuplicateTT.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Typical change areas and safe edits

- If you need to modify API endpoints, update `_serverUrls` in `MainForm.cs` and keep the same `load-brief`, `load`, `save-brief` paths.
- When changing the JSON model shape, update `Models/TimetableItem.cs`. The app relies on the presence of `Width`, `Height` and `GroupIds` when saving.
- Keep JSON naming policy: backend responses use mixed case, so deserialization uses case-insensitive options; save payloads are camelCase.

## Quick examples for the agent

- To add a new server alias, add a key/value to `_serverUrls` and add it to the two ComboBox initializers (`cmbServerLoad.Items.AddRange`, `cmbServerSave.Items.AddRange`).
- To show the full JSON returned by the backend, the app serializes `TimetableItem` with `WriteIndented = true` into `txtJson`.

## Files to inspect for more context

- `MainForm.cs` — UI + HTTP logic
- `Models/TimetableItem.cs` — backend model mapping
- `DuplicateTT.csproj` — target framework and WinForms settings

If anything here is unclear or you want additional patterns included (examples of HTTP error handling, tests, or CI steps), tell me which area to expand and I will iterate.

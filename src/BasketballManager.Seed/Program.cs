using BasketballManager;

// Reset local app data to the built-in demo competition (2 teams x 6 players, no matches).
// Usage:
//   dotnet run --project src/BasketballManager.Seed

var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BasketballManager");
var manager = new CompetitionWorkspaceManager(appDir);
var workspace = manager.ResetToDefaultDemoWorkspace(deleteOtherCompetitions: true);
var store = new DataStore(workspace.DirectoryPath);
var data = store.Load();

Console.WriteLine($"Reset complete.");
Console.WriteLine($"Competition: {workspace.Manifest.Name} ({workspace.Manifest.CompetitionId})");
Console.WriteLine($"Workspace: {workspace.DirectoryPath}");
Console.WriteLine($"Teams: {data.Teams.Count}, Players: {data.Players.Count}, Matches: {data.Matches.Count}");
Console.WriteLine("Launch start_modern_app.bat to verify the built-in demo data.");
return 0;

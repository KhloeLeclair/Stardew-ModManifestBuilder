using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;

namespace Leclair.Stardew.ModManifestBuilder;

public class AddSMAPIDependencyReferencesTask : Task {

	#region Raw Inputs

	/// <summary>
	/// This is an array of SMAPIDependency entries.
	/// </summary>
	[Required]
	public ITaskItem[]? Dependencies { get; set; }

	/// <summary>
	/// This is a path to the mods folder.
	/// </summary>
	[Required]
	public string? ModsPath { get; set; }

	/// <summary>
	/// This is the path to the project file being built.
	/// </summary>
	public string? ProjectPath { get; set; }

	#endregion

	#region Outputs

	[Output]
	public ITaskItem[]? ResolvedReferences { get; set; }

	#endregion

	public override bool Execute() {

		if (Dependencies is null || Dependencies.Length == 0)
			return true;

		Dictionary<string, ITaskItem> references = Dependencies.Where(x => {
			if (x.GetMetadata("Reference").TryParseBoolean(out bool val) && val) {
				if (x.GetMetadata("Required").TryParseBoolean(out bool v) && !v)
					Log.LogAddRef(LogLevel.Error, $"Dependency '{x.ItemSpec}' wants a reference, but is set to not required.", ProjectPath);

				return true;
			}

			return false;
		}).ToDictionary(x => x.ItemSpec, x => x);

		if (references.Count == 0) {
			Log.LogAddRef(LogLevel.Debug, "No <SMAPIDependency /> entries want references.", ProjectPath);
			return true;
		}

		List<ITaskItem> result = new List<ITaskItem>();

		if (string.IsNullOrEmpty(ModsPath) || ! Directory.Exists(ModsPath)) {
			Log.LogAddRef(LogLevel.Error, $"The provided mods directory is empty or missing.", ProjectPath);
			return false;
		}

		bool OnFound(string path, ModManifest manifest, SemanticVersion version) {
			if (references.TryGetValue(manifest.UniqueID!, out var wanted)) {
				references.Remove(manifest.UniqueID!);
				string rawVersion = wanted.GetMetadata("Version");
				SemanticVersion? wantedVersion = null;

				string? from = wanted.GetMetadata("DefiningProjectFullPath");
				if (string.IsNullOrWhiteSpace(from))
					from = ProjectPath!;

				string? dllPath = string.IsNullOrEmpty(manifest.EntryDll)
					? null
					: Path.Combine(path, manifest.EntryDll!);

				if (dllPath is null) {
					Log.LogAddRef(LogLevel.Error, $"The mod '{manifest.UniqueID}' does not have an EntryDll and therefore cannot be used as a reference.", from);

				} else if (!File.Exists(dllPath)) {
					Log.LogAddRef(LogLevel.Error, $"Cannot find EntryDll '{manifest.EntryDll}' for mod '{manifest.UniqueID}'.", from);

				} else if (rawVersion is not null && ! SemanticVersion.TryParse(rawVersion, out wantedVersion)) {
					Log.LogAddRef(LogLevel.Error, $"Could not parse required version '{rawVersion}' for mod '{manifest.UniqueID}'.", from);

				} else if (wantedVersion is not null && wantedVersion.IsNewerThan(version)) {
					Log.LogAddRef(LogLevel.Error, $"Mod '{manifest.UniqueID}' is version '{version}', which does not meet required version '{wantedVersion}'.", from);

				} else {
					Log.LogAddRef(LogLevel.Info, $"Found mod '{manifest.UniqueID}' with version '{version}' at '{dllPath}'.", from);

					string assembly = wanted.GetMetadata("Assembly");
					if (string.IsNullOrEmpty(assembly))
						assembly = Path.GetFileNameWithoutExtension(manifest.EntryDll);

					result.Add(new TaskItem(assembly, new Dictionary<string, string> {
						{ "Private", "False" },
						{ "HintPath", dllPath },
						{ "SMAPIDependency_VersionBehavior", wanted.GetMetadata("VersionBehavior") },
						{ "DefiningProjectFullPath", wanted.GetMetadata("DefiningProjectFullPath") },
						{ "DefiningProjectDirectory", wanted.GetMetadata("DefiningProjectDirectory") },
						{ "DefiningProjectName", wanted.GetMetadata("DefiningProjectName") },
						{ "DefiningProjectExtension", wanted.GetMetadata("DefiningProjectExtension") }
					}));
				}
			}

			// Keep iterating as long as we have references to find.
			return references.Count > 0;
		}

		Utilities.RecursivelyFindMods(ModsPath!, OnFound, checkManifest: false);

		foreach (var entry in references) {
			string? from = entry.Value.GetMetadata("DefiningProjectFullPath");
			if (string.IsNullOrWhiteSpace(from))
				from = ProjectPath!;

			Log.LogAddRef(LogLevel.Error, $"Could not find mod '{entry.Key}' to reference.", from);
		}

		ResolvedReferences = result.ToArray();

		return !Log.HasLoggedErrors;
	}
}

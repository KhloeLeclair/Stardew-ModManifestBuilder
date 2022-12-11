using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
					Log.LogAddRef(LogLevel.Error, $"Dependency '{x.ItemSpec}' wants a reference, but is set to not required.");

				return true;
			}

			return false;
		}).ToDictionary(x => x.ItemSpec, x => x);

		if (references.Count == 0) {
			Log.LogAddRef(LogLevel.Debug, "No <SMAPIDependency /> entries want references.");
			return true;
		}

		List<ITaskItem> result = new List<ITaskItem>();

		if (string.IsNullOrEmpty(ModsPath) || ! Directory.Exists(ModsPath)) {
			Log.LogAddRef(LogLevel.Error, $"The provided mods directory is empty or missing.");
			return false;
		}

		RecursivelyFindMods(ModsPath!, references, result, false);

		foreach (var entry in references)
			Log.LogAddRef(LogLevel.Error, $"Could not find mod '{entry.Key}' to reference.");

		ResolvedReferences = result.ToArray();

		return !Log.HasLoggedErrors;
	}

	private void RecursivelyFindMods(string path, Dictionary<string, ITaskItem> targets, List<ITaskItem> found, bool checkManifest = true) {

		string manifestFile = Path.Combine(path, "manifest.json");

		// If there's a manifest here, don't recurse into it.
		if (File.Exists(manifestFile)) {
			if (checkManifest)
				TryConsumeMod(path, targets, found);

			return;
		}

		if (targets.Count == 0)
			return;

		foreach (string dir in Directory.EnumerateDirectories(path)) {
			string reldir = Utilities.GetRelativePath(path, dir);

			// SMAPI skips folders starting with a dot.
			if (reldir.StartsWith("."))
				continue;

			RecursivelyFindMods(dir, targets, found);
			if (targets.Count == 0)
				return;
		}
	}

	private void TryConsumeMod(string path, Dictionary<string, ITaskItem> targets, List<ITaskItem> found) {
		string manifestFile = Path.Combine(path, "manifest.json");

		if (!File.Exists(manifestFile))
			return;

		ModManifest manifest;
		try {
			manifest = JsonConvert.DeserializeObject<ModManifest>(File.ReadAllText(manifestFile));
		} catch {
			return;
		}

		if (manifest?.UniqueID is null || !targets.TryGetValue(manifest.UniqueID, out ITaskItem wanted))
			return;

		targets.Remove(manifest.UniqueID);

		string? dllPath = string.IsNullOrEmpty(manifest.EntryDll)
						? null
						: Path.Combine(path, manifest.EntryDll!);

		if (dllPath is null) {
			Log.LogAddRef(LogLevel.Error, $"Mod '{manifest.UniqueID}' does not have an EntryDll and therefore cannot be used as a reference.");
			return;
		}

		if (!File.Exists(dllPath)) {
			Log.LogAddRef(LogLevel.Error, $"Cannot find EntryDll '{manifest.EntryDll}' for mod '{manifest.UniqueID}'.");
			return;
		}

		if (!SemanticVersion.TryParse(manifest.Version, out var modVer) || modVer is null) {
			Log.LogAddRef(LogLevel.Error, $"Could not parse version '{manifest.Version}' of mod '{manifest.UniqueID}'.");
			return;
		}

		string? rawWantVer = wanted.GetMetadata("Version");
		if (rawWantVer is null || !SemanticVersion.TryParse(rawWantVer, out var wantVer) || wantVer is null) {
			Log.LogAddRef(LogLevel.Error, $"Could not parse required version '{rawWantVer}' for mod '{manifest.UniqueID}'.");
			return;
		}

		if (wantVer.IsNewerThan(modVer)) {
			Log.LogAddRef(LogLevel.Error, $"Mod '{manifest.UniqueID}' is version '{modVer}', which does not meet required version '{wantVer}'.");
			return;
		}

		Log.LogAddRef(LogLevel.Info, $"Found mod '{manifest.UniqueID}' with version '{modVer}' at '{dllPath}'.");

		string assembly = wanted.GetMetadata("Assembly");
		if (string.IsNullOrEmpty(assembly))
			assembly = Path.GetFileNameWithoutExtension(manifest.EntryDll);

		found.Add(new TaskItem(assembly, new Dictionary<string, string> {
			{ "Private", "False" },
			{ "HintPath", dllPath }
		}));
	}

}

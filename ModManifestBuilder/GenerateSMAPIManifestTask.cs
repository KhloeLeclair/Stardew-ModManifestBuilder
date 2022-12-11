using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;

namespace Leclair.Stardew.ModManifestBuilder;

public enum VersionBehavior {
	Ignore,
	Warning,
	Error,
	Update,
	UpdateFull
};

/// <summary>
/// Generates a SMAPI <c>manifest.json</c> file using values from the
/// project's configuration.
/// </summary>
public class GenerateSMAPIManifestTask : Task {

	#region Raw Inputs

	// Configuration Options
	public bool AlwaysSetEntryDll { get; set; }
	public bool Version_AppendConfiguration { get; set; }
	public string? Dependencies_VersionBehavior { get; set; }
	public string? MinimumApiVersion_Behavior { get; set; }

	// File Path Stuff

	[Required]
	public string? ProjectDir { get; set; }
	[Required]
	public string? AssemblyName { get; set; }
	public string? BaseManifest { get; set; }
	[Required]
	public string? ManifestName { get; set; }
	public string? ModsPath { get; set; }

	// Build Stuff
	public string? Configuration { get; set; }
	public ITaskItem[]? References { get; set; }
	public ITaskItem[]? Dependencies { get; set; }

	// Manifest Keys 
	public string? Name { get; set; }

	public string? Description { get; set; }

	[Required]
	public string? Version { get; set; }

	public string? Authors { get; set; }

	public string? Author { get; set; }

	public string? UniqueId { get; set; }
	
	public string? MinimumApiVersion { get; set; }

	public string[]? UpdateKeys { get; set; }

	#endregion

	#region Input Processing

	private Lazy<VersionBehavior> _DepVersionBehavior => Utilities.GetEnumReader(() => Dependencies_VersionBehavior, VersionBehavior.UpdateFull, VersionBehavior.Error);
	private Lazy<VersionBehavior> _SMAPIVersionBehavior => Utilities.GetEnumReader(() => MinimumApiVersion_Behavior, VersionBehavior.Update, VersionBehavior.Error);

	public VersionBehavior DepVersionBehavior => _DepVersionBehavior.Value;
	public VersionBehavior SMAPIVersionBehavior => _SMAPIVersionBehavior.Value;

	#endregion

	#region Execution

	public override bool Execute() {
		// Parse the <Version>
		SemanticVersion? SemVersion;

		if (string.IsNullOrWhiteSpace(Version)) {
			Log.Log(LogLevel.Error, "<Version> cannot be empty or missing.");
			return false;

		} else if (!SemanticVersion.TryParse(Version, out SemVersion) || SemVersion is null) {
			Log.Log(LogLevel.Error, "<Version> has an invalid value that cannot be parsed as a semantic version.");
			return false;
		}

		// If AppendConfiguration is enabled and we're using a non-Release
		// configuration, append it to the version.
		if (Version_AppendConfiguration && !string.IsNullOrEmpty(Configuration) && !Configuration!.Equals("Release")) {
			if (string.IsNullOrEmpty(SemVersion.Release))
				SemVersion.Release = Configuration;
			else
				SemVersion.Release += $".{Configuration}";
		}

		// Read the version of SMAPI as well as all the mod dependencies we have.
		var (smapiVersion, modReferences) = Utilities.ParseReferences(References, Log);
		if (smapiVersion is null) {
			Log.Log(LogLevel.Error, "Mods must reference StardewModdingAPI. Are you using Pathoschild.Stardew.ModBuildConfig?");
			return false;
		}

		Log.Log(LogLevel.Debug, $"SMAPI Version: {smapiVersion}");

		// Now let's load our manifest. Either start with a fresh object, or
		// load a base manifest (which might also be our target).
		ModManifest manifest;

		if (!string.IsNullOrEmpty(BaseManifest) && !"new".Equals(BaseManifest, StringComparison.OrdinalIgnoreCase)) {
			string basePath = Path.Combine(ProjectDir, BaseManifest);
			if (!File.Exists(basePath)) {
				// Are we just editing the manifest, or are we loading a new one?
				if (!BaseManifest!.Equals(ManifestName)) {
					Log.Log(LogLevel.Error, $"Unable to locate <BaseManifest> at {basePath}");
					return false;
				}

				manifest = new();

			} else {
				try {
					manifest = JsonConvert.DeserializeObject<ModManifest>(File.ReadAllText(basePath));

				} catch (Exception ex) {
					Log.Log(LogLevel.Error, $"Unable to load <BaseManifest>. Details: {ex}");
					return false;
				}
			}

		} else
			manifest = new();

		// Set the version. Always. Use the SemVersion as we might have
		// modified it.
		manifest.Version = SemVersion.ToString();

		// If AlwaySetEntryDll is true, or if EntryDll doesn't have a value,
		// then set it based on the project's AssemblyName.
		if (AlwaysSetEntryDll || string.IsNullOrEmpty(manifest.EntryDll))
			manifest.EntryDll = $"{AssemblyName}.dll";

		// Ensure that we're not working on a content pack.
		if (manifest.ContentPackFor is not null)
			Log.Log(LogLevel.Error, "\"ContentPackFor\" should not have a value. ModManifestBuilder is intended for use with C# mods and not for content packs.");

		// Use <Name> if it was specified. If the manifest has no name at all,
		// then fall back to AssemblyName as a safe value.
		if (!string.IsNullOrEmpty(Name))
			manifest.Name = Name;
		else if (string.IsNullOrEmpty(manifest.Name))
			manifest.Name = AssemblyName;

		// Use <Authors> if it was specified.
		if (!string.IsNullOrWhiteSpace(Authors) && Authors != AssemblyName)
			manifest.Author = Authors;
		else if (!string.IsNullOrWhiteSpace(Author)) {
			Log.Log(LogLevel.Warning, "Using <Author> is deprecated. Please use <Authors> instead.");
			manifest.Author = Author;
		} else if (manifest.Author is null)
			Log.Log(LogLevel.Warning, "No <Authors> is set and \"Author\" is not present in existing manifest.");

		// Use <Description> if it was specified.
		if (!string.IsNullOrWhiteSpace(Description))
			manifest.Description = Description;
		else if (manifest.Description is null)
			Log.Log(LogLevel.Warning, "No <Description> is set and \"Description\" is not present in existing manifest.");

		// Use <UniqueId> if it was specified.
		if (!string.IsNullOrWhiteSpace(UniqueId))
			manifest.UniqueID = UniqueId;
		else if (string.IsNullOrWhiteSpace(manifest.UniqueID))
			Log.Log(LogLevel.Error, "No <UniqueId> specified and \"UniqueID\" is not present in existing manifest. UniqueId is required.");

		// Validate the UniqueId.
		if (!Utilities.IsValidModId(manifest.UniqueID))
			Log.Log(LogLevel.Error, $"The UniqueID '{manifest.UniqueID}' is not a valid mod ID. IDs must only contain A-Z, 0-9, '_', '.', and '-' characters and must not be empty.");

		// Use <MinimumApiVersion> if it was specified.
		if (SMAPIVersionBehavior == VersionBehavior.Update)
			manifest.MinimumApiVersion = smapiVersion.ToShortString();
		else if (SMAPIVersionBehavior == VersionBehavior.UpdateFull)
			manifest.MinimumApiVersion = smapiVersion.ToString();
		else if (!string.IsNullOrWhiteSpace(MinimumApiVersion)) {
			// If the MinimumApiVersion is "auto", then set it to the SMAPI
			// version we're building against. This is just an easier way
			// to set the version behavior to "update".
			if (MinimumApiVersion!.Equals("auto", StringComparison.OrdinalIgnoreCase) || MinimumApiVersion.Equals("automatic", StringComparison.OrdinalIgnoreCase))
				manifest.MinimumApiVersion = smapiVersion.ToShortString();
			else
				manifest.MinimumApiVersion = MinimumApiVersion;
		}

		// Validate MinimumApiVersion
		// Skip this for Update and UpdateFull because those obviously are
		// fine, since we set them.
		if (SMAPIVersionBehavior == VersionBehavior.Error || SMAPIVersionBehavior == VersionBehavior.Warning) {
			if (string.IsNullOrEmpty(manifest.MinimumApiVersion)) {
				Log.Log(
					SMAPIVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
					$"No <MinimumApiVersion> specified and \"MinimumApiVersion\" is not present in existing manifest."
				);

			} else if (!SemanticVersion.TryParse(manifest.MinimumApiVersion, out var minimum) || minimum is null || minimum.IsOlderThan(smapiVersion, onlyMajorMinor: true)) {
				Log.Log(
					SMAPIVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
					$"MinimumApiVersion is set to '{manifest.MinimumApiVersion}' but you're building against SMAPI version '{smapiVersion}', which is newer."
				);
			}
		}

		// Handle dependencies.
		Dictionary<string, ModDependency> manifestDeps;
		if (manifest.Dependencies is null)
			manifestDeps = new();
		else
			manifestDeps = manifest.Dependencies.Where(x => !string.IsNullOrEmpty(x.UniqueID)).ToDictionary(x => x.UniqueID!, x => x);

		// Handle <SMAPIDependency> entries.
		if (Dependencies is not null)
			foreach(var entry in Dependencies) {
				if (string.IsNullOrEmpty(entry.ItemSpec))
					continue;

				if (!manifestDeps.TryGetValue(entry.ItemSpec, out var dep)) {
					dep = new() {
						UniqueID = entry.ItemSpec
					};
					manifestDeps[entry.ItemSpec] = dep;
				}

				string ver = entry.GetMetadata("Version");
				bool? required = entry.GetMetadata("Required").TryParseBoolean(out bool br) ? br : null;
				bool? referenced = entry.GetMetadata("Reference").TryParseBoolean(out br) ? br : null;

				// If a dependency injects a reference, it has to be required.
				if (referenced.HasValue && referenced.Value)
					required = true;

				if (!string.IsNullOrEmpty(ver)) {
					if (SemanticVersion.TryParse(ver, out _))
						dep.MinimumVersion = ver;
					else
						Log.Log(LogLevel.Error, $"Dependency '{entry.ItemSpec}' has invalid version that cannot be parsed '{ver}'.");
				}

				if (required.HasValue)
					dep.IsRequired = required.Value;
			}

		// Handle hard dependencies.
		if (modReferences is not null)
			foreach(var entry in modReferences) {
				var (modMan, modVer) = entry.Value;
				if (modMan is null || modVer is null || string.IsNullOrEmpty(modMan.UniqueID))
					continue;

				if (!manifestDeps.TryGetValue(modMan.UniqueID!, out var dep)) {
					dep = new() {
						UniqueID = modMan.UniqueID,
						MinimumVersion = modMan.Version,
						IsRequired = true
					};
					manifestDeps[modMan.UniqueID!] = dep;
				}

				if (!dep.IsRequired.HasValue || ! dep.IsRequired.Value) {
					Log.Log(LogLevel.Warning, $"Dependency '{modMan.UniqueID}' was not set as required, despite a hard reference. Changing to required.");
					dep.IsRequired = true;
				}

				if (string.IsNullOrEmpty(dep.MinimumVersion) || ! SemanticVersion.TryParse(dep.MinimumVersion, out var depVer) || depVer is null || depVer.IsOlderThan(modVer)) {
					switch(DepVersionBehavior) {
						case VersionBehavior.Warning:
						case VersionBehavior.Error:
							Log.Log(
								DepVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
								$"MinimumVersion for referenced dependency '{modMan.UniqueID}' is set to '{dep.MinimumVersion}' but you're building against version '{modVer}', which is newer."
							);
							break;
						case VersionBehavior.Update:
							dep.MinimumVersion = modVer.ToShortString();
							break;
						case VersionBehavior.UpdateFull:
							dep.MinimumVersion = modVer.ToString();
                            break;
					}
				}
			}

		manifest.Dependencies = manifestDeps.Count == 0 ? null : manifestDeps.Values.OrderBy(x => x.UniqueID).ToArray();

		// Use <UpdateKeys> if it was specified.
		if (UpdateKeys != null) {
			string[] keys = UpdateKeys
				.SelectMany(x => string.IsNullOrEmpty(x) ? Array.Empty<string>() : x.Split(','))
				.Select(x => x.Trim())
				.Where(x => ! string.IsNullOrEmpty(x))
				.Distinct()
				.ToArray();

			foreach (string key in keys) {
				if (!Utilities.TryParseUpdateKey(key, out var parsed) || parsed is null) {
					Log.Log(LogLevel.Warning, $"UpdateKey '{key}' is not correctly formatted. See https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Update_checks for details.");
					continue;
				}

				string site = parsed.Value.Item1;
				string id = parsed.Value.Item2;

				switch(site.ToLower()) {
					case "chucklefish":
						Log.Log(LogLevel.Warning, $"UpdateKey '{key}' is using deprecated provider '{site}'.");
						if (!uint.TryParse(id, out _))
							Log.Log(LogLevel.Warning, $"UpdateKey '{key}' has an invalid mod ID. Provider '{site}' uses integer IDs, but '{id}' is not an integer.");
						break;
					case "curseforge":
					case "moddrop":
					case "nexus":
						if (!uint.TryParse(id, out _))
							Log.Log(LogLevel.Warning, $"UpdateKey '{key}' has an invalid mod ID. Provider '{site}' uses integer IDs, but '{id}' is not an integer.");
						break;
					case "github":
						int idx = id.IndexOf("/", StringComparison.OrdinalIgnoreCase);
						if (idx == -1 || idx != id.LastIndexOf("/", StringComparison.OrdinalIgnoreCase))
							Log.Log(LogLevel.Warning, $"UpdateKey '{key}' has an invalid GitHub repository key. Must be a username and project name, like: 'Pathoschild/SMAPI'.");
						break;
					case "updatemanifest":
						try {
							new Uri(id);
						} catch {
							Log.Log(LogLevel.Warning, $"UpdateKey '{key}' has an invalid URL.");
						}
						break;
					case "nexusmods":
						Log.Log(LogLevel.Warning, $"UpdateKey '{key}' has an unknown provider '{site}'. Did you mean 'Nexus'?");
						break;
					default:
						Log.Log(LogLevel.Warning, $"UpdateKey '{key}' has an unknown provider '{site}'. ModManifestBuilder may be out of date, or you may have made a mistake.");
						break;
				}
			}

			manifest.UpdateKeys = keys.Length > 0 ? keys : null;
			if (manifest.UpdateKeys is null)
				Log.Log(LogLevel.Warning, "<UpdateKeys> was present but contained no valid update keys.");

		} else if (manifest.UpdateKeys is null)
			Log.Log(LogLevel.Warning, "No <UpdateKeys> is set and \"UpdateKeys\" is not present in existing manifest.");
		else if (manifest.UpdateKeys.Length == 0)
			Log.Log(LogLevel.Warning, "No <UpdateKeys> is set and existing \"UpdateKeys\" in manifest is empty.");

		manifest.JsonSchema = "https://smapi.io/schemas/manifest.json";

		// Now save the result.
		string result = JsonConvert.SerializeObject(manifest, Formatting.Indented, new JsonSerializerSettings() {
			NullValueHandling = NullValueHandling.Ignore,
		});

		string filename = Path.Combine(ProjectDir, ManifestName);
		File.WriteAllText(filename, result);

		return !Log.HasLoggedErrors;
	}

	#endregion

}

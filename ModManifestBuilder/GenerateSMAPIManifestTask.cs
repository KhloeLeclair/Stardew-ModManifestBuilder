using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;

namespace Leclair.Stardew.ModManifestBuilder;

public enum VersionBehavior {
	Ignore,
	Warning,
	Error,
	Update,
	UpdateNoPrerelease,
	UpdateFull,
	Set,
	SetNoPrerelease,
	SetFull
};

public enum CPVersionBehavior {
	Ignore,
	Set,
	Update,
	Read
};


/// <summary>
/// Generates a SMAPI <c>manifest.json</c> file using values from the
/// project's configuration.
/// </summary>
public class GenerateSMAPIManifestTask : Task {

	#region Raw Inputs

	// Configuration Options
	public bool AlwaysSetEntryDll { get; set; }
	public bool Dependencies_AlwaysIncludeRequired { get; set; }
	public string? Dependencies_VersionBehavior { get; set; }
	public bool ManifestComment { get; set; }
	public string? ManifestSchema { get; set; }
	public string? MinimumApiVersion_Behavior { get; set; }
	public string? MinimumGameVersion_Behavior { get; set; }
	public string? References_VersionBehavior { get; set; }
	public bool Version_AppendConfiguration { get; set; }
	public string? ContentPacks_VersionBehavior { get; set; }

	public bool ManifestWarningsAsErrors { get; set; }

	// File Path Stuff

	[Required]
	public string? ProjectDir { get; set; }
	[Required]
	public string? AssemblyName { get; set; }
	public string? BaseManifest { get; set; }
	[Required]
	public string? ManifestName { get; set; }
	public string? GamePath { get; set; }
	public string? ModsPath { get; set; }
	public string? ProjectPath { get; set; }

	// Build Stuff
	public string? Configuration { get; set; }
	public ITaskItem[]? References { get; set; }
	public ITaskItem[]? Dependencies { get; set; }

	public ITaskItem[]? ContentPacks { get; set; }

	[Output]
	public ITaskItem[]? UpdatedContentPacks { get; set; }

	// Manifest Keys 
	public string? Name { get; set; }

	public string? Description { get; set; }

	[Required]
	public string? Version { get; set; }

	public string? Authors { get; set; }

	public string? Author { get; set; }

	public string? UniqueId { get; set; }

	public string? MinimumApiVersion { get; set; }

	public string? MinimumGameVersion { get; set; }

	public string[]? UpdateKeys { get; set; }

	#endregion

	#region Input Processing

	private Lazy<VersionBehavior> _DepVersionBehavior => Utilities.GetEnumReader(() => Dependencies_VersionBehavior, VersionBehavior.Warning, VersionBehavior.Error);
	private Lazy<VersionBehavior> _RefVersionBehavior => Utilities.GetEnumReader(() => References_VersionBehavior, VersionBehavior.UpdateNoPrerelease, VersionBehavior.Error);
	private Lazy<VersionBehavior> _SMAPIVersionBehavior => Utilities.GetEnumReader(() => MinimumApiVersion_Behavior, VersionBehavior.Update, VersionBehavior.Error);
	private Lazy<VersionBehavior> _GameVersionBehavior => Utilities.GetEnumReader(() => MinimumGameVersion_Behavior, VersionBehavior.Update, VersionBehavior.Error);
	private Lazy<CPVersionBehavior> _ContentPacks_VersionBehavior => Utilities.GetEnumReader(() => ContentPacks_VersionBehavior, CPVersionBehavior.Set, CPVersionBehavior.Ignore);

	public VersionBehavior DepVersionBehavior => _DepVersionBehavior.Value;
	public VersionBehavior RefVersionBehavior => _RefVersionBehavior.Value;
	public VersionBehavior SMAPIVersionBehavior => _SMAPIVersionBehavior.Value;
	public VersionBehavior GameVersionBehavior => _GameVersionBehavior.Value;

	public CPVersionBehavior CPVersionBehavior => _ContentPacks_VersionBehavior.Value;

	#endregion

	#region Execution

	public override bool Execute() {
		// If this is set, upgrade all our warnings to errors.
		Utilities.ManifestWarningsAsErrors = ManifestWarningsAsErrors;

		// Parse the <Version>
		SemanticVersion? SemVersion;

		if (string.IsNullOrWhiteSpace(Version)) {
			Log.LogGen(LogLevel.Error, "<Version> cannot be empty or missing.", ProjectPath);
			return false;

		} else if (!SemanticVersion.TryParse(Version, out SemVersion) || SemVersion is null) {
			Log.LogGen(LogLevel.Error, "<Version> has an invalid value that cannot be parsed as a semantic version.", ProjectPath);
			return false;
		}

		// If AppendConfiguration is enabled and we're using a non-Release
		// configuration, append it to the version.
		if (Version_AppendConfiguration && !string.IsNullOrEmpty(Configuration) && !Configuration!.Equals("Release")) {
			if (string.IsNullOrEmpty(SemVersion.Prerelease))
				SemVersion.Prerelease = Configuration;
			else
				SemVersion.Prerelease += $".{Configuration}";
		}

		// Read the version of SMAPI as well as all the mod dependencies we have.
		var (smapiVersion, gameVersion, modReferences) = Utilities.ParseReferences(References, GamePath, Log);
		if (smapiVersion is null || gameVersion is null) {
			Log.LogGen(LogLevel.Error, "Mods must reference StardewModdingAPI and StardewValley. Are you using Pathoschild.Stardew.ModBuildConfig?", ProjectPath);
			return false;
		}

		Log.LogGen(LogLevel.Debug, $"SMAPI Version: {smapiVersion}", ProjectPath);
		Log.LogGen(LogLevel.Debug, $"Game Version: {gameVersion}", ProjectPath);

		// Now let's load our manifest. Either start with a fresh object, or
		// load a base manifest (which might also be our target).
		ModManifest manifest;
		bool generated;

		if (!string.IsNullOrEmpty(BaseManifest) && !"new".Equals(BaseManifest, StringComparison.OrdinalIgnoreCase)) {
			string basePath = Path.Combine(ProjectDir, BaseManifest);
			generated = !BaseManifest!.Equals(ManifestName, StringComparison.OrdinalIgnoreCase);

			if (!File.Exists(basePath)) {
				// Are we just editing the manifest, or are we loading a new one?
				if (generated) {
					Log.LogGen(LogLevel.Error, $"Unable to locate <BaseManifest> at {basePath}", ProjectPath);
					return false;
				}

				manifest = new();

			} else {
				try {
					manifest = JsonConvert.DeserializeObject<ModManifest>(File.ReadAllText(basePath));

				} catch (Exception ex) {
					Log.LogGen(LogLevel.Error, $"Unable to load <BaseManifest>. Details: {ex}", ProjectPath);
					return false;
				}
			}

		} else {
			manifest = new();
			generated = true;
		}

		// If setFields is given a value, then we track the fields that get
		// a value assigned to them. Even if that value is the same as the
		// existing value.
		HashSet<string>? setFields = null; // generated ? null : new();

		// Set the version. Always. Use the SemVersion as we might have
		// modified it.
		manifest.Version = SemVersion.ToString();
		setFields?.Add(nameof(manifest.Version));

		// If AlwaySetEntryDll is true, or if EntryDll doesn't have a value,
		// then set it based on the project's AssemblyName.
		if (AlwaysSetEntryDll || string.IsNullOrEmpty(manifest.EntryDll)) {
			manifest.EntryDll = $"{AssemblyName}.dll";
			setFields?.Add(nameof(manifest.EntryDll));
		}

		// Ensure that we're not working on a content pack.
		if (manifest.ContentPackFor is not null)
			Log.LogGen(LogLevel.Error, "\"ContentPackFor\" should not have a value. ModManifestBuilder is intended for use with C# mods and not for content packs.");

		// Use <Name> if it was specified. If the manifest has no name at all,
		// then fall back to AssemblyName as a safe value.
		if (!string.IsNullOrEmpty(Name)) {
			manifest.Name = Name;
			setFields?.Add(nameof(manifest.Name));
		} else if (string.IsNullOrEmpty(manifest.Name))
			manifest.Name = AssemblyName;

		// Use <Authors> if it was specified.
		if (!string.IsNullOrWhiteSpace(Authors) && Authors != AssemblyName) {
			manifest.Author = Authors;
			setFields?.Add(nameof(manifest.Author));
		} else if (!string.IsNullOrWhiteSpace(Author)) {
			Log.LogGen(LogLevel.Warning, "Using <Author> is deprecated. Please use <Authors> instead.", ProjectPath);
			manifest.Author = Author;
			setFields?.Add(nameof(manifest.Author));
		} else if (manifest.Author is null)
			Log.LogGen(LogLevel.Warning, "No <Authors> is set and \"Author\" is not present in existing manifest.", ProjectPath);

		// Use <Description> if it was specified.
		if (!string.IsNullOrWhiteSpace(Description)) {
			manifest.Description = Description;
			setFields?.Add(nameof(manifest.Description));
		} else if (manifest.Description is null)
			Log.LogGen(LogLevel.Warning, "No <Description> is set and \"Description\" is not present in existing manifest.", ProjectPath);

		// Use <UniqueId> if it was specified.
		if (!string.IsNullOrWhiteSpace(UniqueId)) {
			manifest.UniqueID = UniqueId;
			setFields?.Add(nameof(manifest.UniqueID));
		} else if (string.IsNullOrWhiteSpace(manifest.UniqueID))
			Log.LogGen(LogLevel.Error, "No <UniqueId> specified and \"UniqueID\" is not present in existing manifest. UniqueId is required.", ProjectPath);

		// Validate the UniqueId.
		if (!Utilities.IsValidModId(manifest.UniqueID))
			Log.LogGen(LogLevel.Error, $"The UniqueID '{manifest.UniqueID}' is not a valid mod ID. IDs must only contain A-Z, 0-9, '_', '.', and '-' characters and must not be empty.");

		// Use <MinimumApiVersion> if it was specified.
		switch (SMAPIVersionBehavior) {
			case VersionBehavior.Update:
			case VersionBehavior.Set:
				manifest.MinimumApiVersion = smapiVersion.ToShortString();
				setFields?.Add(nameof(manifest.MinimumApiVersion));
				break;
			case VersionBehavior.UpdateNoPrerelease:
			case VersionBehavior.SetNoPrerelease:
				manifest.MinimumApiVersion = smapiVersion.ToNoPrereleaseString();
				setFields?.Add(nameof(manifest.MinimumApiVersion));
				break;
			case VersionBehavior.UpdateFull:
			case VersionBehavior.SetFull:
				manifest.MinimumApiVersion = smapiVersion.ToString();
				setFields?.Add(nameof(manifest.MinimumApiVersion));
				break;
			default:
				if (!string.IsNullOrWhiteSpace(MinimumApiVersion)) {
					// If the MinimumApiVersion is "auto", then set it to the SMAPI
					// version we're building against. This is just an easier way
					// to set the version behavior to "update".
					if (MinimumApiVersion!.Equals("auto", StringComparison.OrdinalIgnoreCase) || MinimumApiVersion.Equals("automatic", StringComparison.OrdinalIgnoreCase))
						manifest.MinimumApiVersion = smapiVersion.ToShortString();
					else
						manifest.MinimumApiVersion = MinimumApiVersion;

					setFields?.Add(nameof(manifest.MinimumApiVersion));
				}
				break;
		}

		// Validate MinimumApiVersion
		// Skip this for Update and UpdateFull because those obviously are
		// fine, since we set them.
		if (SMAPIVersionBehavior == VersionBehavior.Error || SMAPIVersionBehavior == VersionBehavior.Warning) {
			if (string.IsNullOrEmpty(manifest.MinimumApiVersion)) {
				Log.LogGen(
					SMAPIVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
					$"No <MinimumApiVersion> specified and \"MinimumApiVersion\" is not present in existing manifest.",
					ProjectPath
				);

			} else if (!SemanticVersion.TryParse(manifest.MinimumApiVersion, out var minimum) || minimum is null || minimum.IsOlderThan(smapiVersion, onlyMajorMinor: true)) {
				Log.LogGen(
					SMAPIVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
					$"MinimumApiVersion is set to '{manifest.MinimumApiVersion}' but you're building against SMAPI version '{smapiVersion}', which is newer.",
					ProjectPath
				);
			}
		}

		// Use <MinimumGameVersion> if it was specified.
		switch (GameVersionBehavior) {
			case VersionBehavior.Update:
			case VersionBehavior.Set:
				manifest.MinimumGameVersion = gameVersion.ToNoRevisionString(false);
				setFields?.Add(nameof(manifest.MinimumGameVersion));
				break;
			case VersionBehavior.UpdateNoPrerelease:
			case VersionBehavior.SetNoPrerelease:
				manifest.MinimumGameVersion = gameVersion.ToNoRevisionString(true, false);
				setFields?.Add(nameof(manifest.MinimumGameVersion));
				break;
			case VersionBehavior.UpdateFull:
			case VersionBehavior.SetFull:
				manifest.MinimumGameVersion = gameVersion.ToNoRevisionString(true, true);
				setFields?.Add(nameof(manifest.MinimumGameVersion));
				break;
			default:
				if (!string.IsNullOrWhiteSpace(MinimumGameVersion)) {
					// If the MinimumGameVersion is "auto", then set it to the game
					// version we're building against. This is just an easier way
					// to set the version behavior to "update".
					if (MinimumGameVersion!.Equals("auto", StringComparison.OrdinalIgnoreCase) || MinimumGameVersion.Equals("automatic", StringComparison.OrdinalIgnoreCase))
						manifest.MinimumGameVersion = gameVersion.ToNoRevisionString(true, true);
					else
						manifest.MinimumGameVersion = MinimumGameVersion;

					setFields?.Add(nameof(manifest.MinimumGameVersion));
				}
				break;
		}

		// Validate MinimumGameVersion
		// Skip this for Update and UpdateFull because those obviously are
		// fine, since we set them.
		if (GameVersionBehavior == VersionBehavior.Error || GameVersionBehavior == VersionBehavior.Warning) {
			if (string.IsNullOrEmpty(manifest.MinimumGameVersion)) {
				Log.LogGen(
					GameVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
					$"No <MinimumGameVersion> specified and \"MinimumGameVersion\" is not present in existing manifest.",
					ProjectPath
				);

			} else if (!SemanticVersion.TryParse(manifest.MinimumGameVersion, out var minimum) || minimum is null || minimum.IsOlderThan(gameVersion, onlyMajorMinor: true)) {
				Log.LogGen(
					SMAPIVersionBehavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
					$"MinimumGameVersion is set to '{manifest.MinimumGameVersion}' but you're building against game version '{gameVersion.ToNoRevisionString(true, true)}', which is newer.",
					ProjectPath
				);
			}
		}

		// Handle dependencies.
		Dictionary<string, ModDependency> manifestDeps;
		if (manifest.Dependencies is null)
			manifestDeps = new();
		else
			manifestDeps = manifest.Dependencies.Where(x => !string.IsNullOrEmpty(x.UniqueID)).ToDictionary(x => x.UniqueID!, x => x);

		Dictionary<string, VersionBehavior?> checkBehaviors = new();

		// Handle <SMAPIDependency> entries.
		if (Dependencies is not null)
			foreach (var entry in Dependencies) {
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

				// If we have a VersionBehavior override, store it for later.
				if (entry.GetMetadata("VersionBehavior").TryParseEnum(out VersionBehavior vb))
					checkBehaviors[entry.ItemSpec] = vb;

				// If a dependency injects a reference, it has to be required.
				if (referenced.HasValue && referenced.Value)
					required = true;

				if (!string.IsNullOrEmpty(ver)) {
					if (SemanticVersion.TryParse(ver, out _))
						dep.MinimumVersion = ver;
					else
						Log.LogGen(LogLevel.Error, $"Dependency '{entry.ItemSpec}' has invalid version that cannot be parsed '{ver}'.", ProjectPath);
				}

				if (required.HasValue)
					dep.IsRequired = required.Value;

				// IsRequired is the default.
				if (dep.IsRequired.HasValue && dep.IsRequired.Value && !Dependencies_AlwaysIncludeRequired)
					dep.IsRequired = null;
				else if (!dep.IsRequired.HasValue && Dependencies_AlwaysIncludeRequired)
					dep.IsRequired = true;

				setFields?.Add(nameof(manifest.Dependencies));
			}

		// Handle hard dependencies.
		if (modReferences is not null)
			foreach (var entry in modReferences) {
				var (modMan, modVer, verBehavior) = entry.Value;
				if (modMan is null || modVer is null || string.IsNullOrEmpty(modMan.UniqueID))
					continue;

				VersionBehavior behavior = verBehavior ?? RefVersionBehavior;

				if (!manifestDeps.TryGetValue(modMan.UniqueID!, out var dep)) {
					dep = new() {
						UniqueID = modMan.UniqueID,
						IsRequired = Dependencies_AlwaysIncludeRequired ? true : null
					};

					switch (behavior) {
						case VersionBehavior.Set:
						case VersionBehavior.Update:
							dep.MinimumVersion = modVer.ToShortString();
							break;
						case VersionBehavior.SetNoPrerelease:
						case VersionBehavior.UpdateNoPrerelease:
							dep.MinimumVersion = modVer.ToNoPrereleaseString();
							break;
						case VersionBehavior.SetFull:
						case VersionBehavior.UpdateFull:
							dep.MinimumVersion = modVer.ToString();
							break;
					}

					manifestDeps[modMan.UniqueID!] = dep;
				}

				setFields?.Add(nameof(manifest.Dependencies));

				if (!dep.IsRequired.HasValue && Dependencies_AlwaysIncludeRequired) {
					Log.LogGen(LogLevel.Info, $"Dependency '{modMan.UniqueID}' did not have IsRequired set. Changing to true.", ProjectPath);
					dep.IsRequired = true;

				} else if (dep.IsRequired.HasValue && !dep.IsRequired.Value) {
					Log.LogGen(LogLevel.Warning, $"Dependency '{modMan.UniqueID}' was set as not required, despite having a hard reference. Changing to required.", ProjectPath);
					dep.IsRequired = Dependencies_AlwaysIncludeRequired ? true : null;
				}

				// Store the appropriate version checking behavior for later.
				checkBehaviors[modMan.UniqueID!] = behavior;
			}

		manifest.Dependencies = manifestDeps.Count == 0 ? null : manifestDeps.Values.OrderBy(x => x.UniqueID).ToArray();

		// Validate final dependencies.
		if (manifest.Dependencies is not null) {
			var mods = Utilities.DiscoverMods(ModsPath);

			foreach (var entry in manifest.Dependencies) {
				if (string.IsNullOrEmpty(entry.UniqueID))
					continue;

				if (!mods.TryGetValue(entry.UniqueID!, out var modData)) {
					Log.LogGen(LogLevel.Info, $"Dependency '{entry.UniqueID}' is not present within the game's mod directory.", ProjectPath);
					continue;
				}

				VersionBehavior behavior;
				if (checkBehaviors.TryGetValue(entry.UniqueID!, out VersionBehavior? vb)) {
					if (!vb.HasValue)
						continue;
					behavior = vb.Value;
				} else
					behavior = DepVersionBehavior;

				if (behavior == VersionBehavior.Ignore)
					continue;


				SemanticVersion? depVer = null;
				if (!string.IsNullOrEmpty(entry.MinimumVersion))
					SemanticVersion.TryParse(entry.MinimumVersion, out depVer);

				if (depVer is null ||
					(behavior == VersionBehavior.Set || behavior == VersionBehavior.SetNoPrerelease || behavior == VersionBehavior.SetFull) ||
					depVer.IsOlderThan(modData.Item2, onlyMajorMinor: behavior == VersionBehavior.Update)
				) {
					switch (behavior) {
						case VersionBehavior.Warning:
						case VersionBehavior.Error:
							if (string.IsNullOrEmpty(entry.MinimumVersion))
								Log.LogGen(
									behavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
									$"MinimumVersion for dependency '{entry.UniqueID}' is not set. The installed version is '{modData.Item2}'.",
									ProjectPath
								);
							else
								Log.LogGen(
									behavior == VersionBehavior.Error ? LogLevel.Error : LogLevel.Warning,
									$"MinimumVersion for dependency '{entry.UniqueID}' is set to '{entry.MinimumVersion}'. The installed version is '{modData.Item2}', which is newer.",
									ProjectPath
								);
							break;
						case VersionBehavior.Set:
						case VersionBehavior.Update:
							entry.MinimumVersion = modData.Item2.ToShortString();
							break;
						case VersionBehavior.SetNoPrerelease:
						case VersionBehavior.UpdateNoPrerelease:
							entry.MinimumVersion = modData.Item2.ToNoPrereleaseString();
							break;
						case VersionBehavior.SetFull:
						case VersionBehavior.UpdateFull:
							entry.MinimumVersion = modData.Item2.ToString();
							break;
					}
				}
			}
		}

		// Use <UpdateKeys> if it was specified.
		if (UpdateKeys != null) {
			var processingKeys = UpdateKeys
				.SelectMany(x => string.IsNullOrEmpty(x) ? Array.Empty<string>() : x.Split(','))
				.Select(x => x.Trim())
				.Where(x => !string.IsNullOrEmpty(x));

			if (manifest.UpdateKeys is not null && manifest.UpdateKeys.Length > 0)
				processingKeys = processingKeys.Concat(manifest.UpdateKeys);

			string[] keys = processingKeys.Distinct().ToArray();
			manifest.UpdateKeys = keys.Length > 0 ? keys : null;

			if (manifest.UpdateKeys is null)
				Log.LogGen(LogLevel.Warning, "<UpdateKeys> was present but contained no values.", ProjectPath);

			setFields?.Add(nameof(manifest.UpdateKeys));

		} else if (manifest.UpdateKeys is null)
			Log.LogGen(LogLevel.Warning, "No <UpdateKeys> is set and \"UpdateKeys\" is not present in existing manifest.", ProjectPath);
		else if (manifest.UpdateKeys.Length == 0)
			Log.LogGen(LogLevel.Warning, "No <UpdateKeys> is set and existing \"UpdateKeys\" in manifest is empty.", ProjectPath);

		// Validate individual update keys.
		if (manifest.UpdateKeys is not null) {
			foreach (string key in manifest.UpdateKeys) {
				if (!Utilities.TryParseUpdateKey(key, out var parsed) || parsed is null) {
					Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' is not correctly formatted. See https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Update_checks for details.", ProjectPath);
					continue;
				}

				string site = parsed.Value.Item1;
				string id = parsed.Value.Item2;

				switch (site.ToLower()) {
					case "chucklefish":
						Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' is using deprecated provider '{site}'.", ProjectPath);
						if (!uint.TryParse(id, out _))
							Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an invalid mod ID. Provider '{site}' uses integer IDs, but '{id}' is not an integer.", ProjectPath);
						break;
					case "curseforge":
					case "moddrop":
					case "nexus":
						if (!uint.TryParse(id, out _))
							Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an invalid mod ID. Provider '{site}' uses integer IDs, but '{id}' is not an integer.", ProjectPath);
						break;
					case "github":
						int idx = id.IndexOf("/", StringComparison.OrdinalIgnoreCase);
						if (idx == -1 || idx != id.LastIndexOf("/", StringComparison.OrdinalIgnoreCase))
							Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an invalid GitHub repository key. Must be a username and project name, like: 'Pathoschild/SMAPI'.", ProjectPath);
						if (!string.IsNullOrEmpty(parsed.Value.Item3))
							Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an update subkey, but subkeys don't work for GitHub repositories due to how releases are fetched.", ProjectPath);
						break;
					case "updatemanifest":
						try {
							new Uri(id);
						} catch {
							Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an invalid URL.", ProjectPath);
						}
						break;
					case "nexusmods":
						Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an unknown provider '{site}'. Did you mean 'Nexus'?", ProjectPath);
						break;
					default:
						Log.LogGen(LogLevel.Warning, $"UpdateKey '{key}' has an unknown provider '{site}'. ModManifestBuilder may be out of date, or you may have made a mistake.", ProjectPath);
						break;
				}
			}
		}

		// Set the "$schema" property if desired.
		if (!string.IsNullOrEmpty(ManifestSchema)) {
			if (ManifestSchema.TryParseBoolean(out bool value)) {
				if (value)
					manifest.JsonSchema = "https://smapi.io/schemas/manifest.json";
			} else {
				try {
					new Uri(ManifestSchema);
					manifest.JsonSchema = ManifestSchema;
				} catch {
					Log.LogGen(LogLevel.Error, $"ManifestSchema '{ManifestSchema}' has an invalid value. Must be boolean or a valid URL.", ProjectPath);
				}
			}
		}

		// Now save the result.
		string result = JsonConvert.SerializeObject(manifest, Formatting.Indented, new JsonSerializerSettings() {
			NullValueHandling = NullValueHandling.Ignore
		});

		// If a comment hasn't been disabled, we want to inject it into the JSON result we just saved.
		// We do this manually because JsonConvert doesn't support comment blocks.
		if (ManifestComment) {
			string?[] comment;
			if (generated)
				comment = new string?[] {
					"This file is automatically generated by ModManifestBuilder",
					"when the project is compiled.",
					"",
					"Do not change this file directly."
				};
			else
				comment = new string?[] {
					"This file is automatically updated by ModManifestBuilder",
					"when the project is compiled.",
					"",
					setFields is null
						? "Changes made to this file may be overwritten."
						: $"Changes made to the following fields may be overwritten:",
					setFields is null ? null : $"  {string.Join(", ", setFields.OrderBy(x => x))}",
					""
				};

			// Find the line separator, and make sure we know what it is.
			string newline = Environment.NewLine;
			int idx = result.IndexOf(newline);
			if (idx == -1) {
				newline = "\n";
				idx = result.IndexOf(newline);
			}

			if (idx != -1) {
				idx += newline.Length;

				// Store the whitespace used to indent.
				string whitespace = string.Empty;
				int i = idx;
				while (i < result.Length && char.IsWhiteSpace(result[i])) {
					whitespace += result[i];
					i++;
				}

				string joined = string.Join(newline, comment.Where(x => x is not null).Select(x => $"{whitespace} | {x}"));
				result = result.Substring(0, idx) + whitespace + "/*" + newline + joined + newline + whitespace + " */" + newline + result.Substring(idx);
			}
		}

		string filename = Path.Combine(ProjectDir, ManifestName);
		File.WriteAllText(filename, result);

		// Let's update our content packs too.
		if (ContentPacks != null) {
			UpdatedContentPacks = new ITaskItem[ContentPacks.Length];
			for (int i = 0; i < ContentPacks.Length; i++) {
				var item = ContentPacks[i];
				UpdatedContentPacks[i] = item;

				string itemVer = item.GetMetadata("Version");
				CPVersionBehavior behavior;
				if (!string.IsNullOrWhiteSpace(itemVer)) {
					if (!Enum.TryParse(itemVer, out behavior))
						continue;
				} else
					behavior = CPVersionBehavior;

				// We do nothing for a content pack if the version is not set to Set, Update, or Read.
				if (behavior != CPVersionBehavior.Set && behavior != CPVersionBehavior.Update && behavior != CPVersionBehavior.Read)
					continue;

				// ModBuildConfig validates these. We just want to
				// find the manifest and update the manifest.
				if (string.IsNullOrWhiteSpace(item.ItemSpec))
					continue;

				// Use the same logic to find the manifest that ModBuildConfig does.
				string folderName = item.GetMetadata("FolderName");
				if (string.IsNullOrWhiteSpace(folderName))
					folderName = Path.GetFileName(item.ItemSpec);

				string maniPath = Path.Combine(ProjectDir, folderName, "manifest.json");
				if (!File.Exists(maniPath)) {
					Log.LogGen(LogLevel.Warning, $"Unable to find manifest for bundled content pack '{item.ItemSpec}'.");
					continue;
				}

				ModManifest packManifest;
				try {
					packManifest = JsonConvert.DeserializeObject<ModManifest>(File.ReadAllText(maniPath));

				} catch (Exception ex) {
					Log.LogGen(LogLevel.Error, $"Unable to load manifest for bundled content pack '{item.ItemSpec}' from '{maniPath}'. Details: {ex}", ProjectPath);
					continue;
				}

				if (behavior == CPVersionBehavior.Read) {
					Log.LogGen(LogLevel.Debug, $"Read version '{packManifest.Version} for '{item.ItemSpec}'.");
					item.SetMetadata("Version", packManifest.Version);

				} else {
					if (packManifest.Version != Version) {
						packManifest.Version = Version;

						// Now save the result.
						string packResult = JsonConvert.SerializeObject(packManifest, Formatting.Indented, new JsonSerializerSettings() {
							NullValueHandling = NullValueHandling.Ignore
						});

						// And actually save it.
						File.WriteAllText(maniPath, packResult);

						Log.LogGen(LogLevel.Info, $"Setting version for '{item.ItemSpec}' in '{maniPath}.");
					}

					// Update the metadata since we got this far.
					item.SetMetadata("Version", Version);
				}

				UpdatedContentPacks[i] = item;
			}
		}

		return !Log.HasLoggedErrors;
	}

	#endregion

}

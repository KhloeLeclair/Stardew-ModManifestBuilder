using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Leclair.Stardew.ModManifestBuilder;

/// <summary>
/// Generates a SMAPI <c>manifest.json</c> file using values from the
/// project's configuration.
/// </summary>
public class GenerateSMAPIManifestTask : Task { 

	// File Path Stuff

	[Required]
	public string ProjectDir { get; set; }
	[Required]
	public string AssemblyName { get; set; }
	public string BaseManifest { get; set; }
	[Required]
	public string ManifestName { get; set; }
	public string Configuration { get; set; }
	public string Version_AppendConfiguration { get; set; }

	// Manifest Keys 
	public string Name { get; set; }

	public string Description { get; set; }

	[Required]
	public string Version { get; set; }

	public string Author { get; set; }

	public string UniqueId { get; set; }
	
	public string MinimumApiVersion { get; set; }

	public string UpdateKeys { get; set; }


	public override bool Execute() {

		if (string.IsNullOrEmpty(Version)) {
			Log.LogError($"[generate smapi manifest] <Version> cannot be empty or missing.");
			return false;
		}

		// If using a non-Release Configuration, and the Version doesn't
		// already have a pre-release version specified, include the
		// Configuration there.
		if (Version_AppendConfiguration == "true" && !string.IsNullOrEmpty(Configuration) && !Configuration.Equals("Release")) {

			int preStart = Version.IndexOf('-');
			int buildStart = Version.IndexOf('+');

			if (preStart == -1) {
				preStart = buildStart;
				if (preStart == -1)
					preStart = Version.Length;
			}

			string prefix = Version.Substring(0, preStart);
			string suffix = buildStart == -1 ? "" : Version.Substring(buildStart);

			string prerelease;
			if (buildStart == -1)
				prerelease = Version.Substring(preStart);
			else
				prerelease = Version.Substring(preStart, buildStart - preStart);

			if (string.IsNullOrEmpty(prerelease))
				prerelease = $"-{Configuration}";
			else
				prerelease = $"{prerelease}.{Configuration}";

			Version = $"{prefix}{prerelease}{suffix}";
		}

		// Either start with a fresh object, or load the existing / base
		// manifest file.
		JObject obj;

		if (!string.IsNullOrEmpty(BaseManifest)) {
			string basePath = Path.Combine(ProjectDir, BaseManifest);
			if (!File.Exists(basePath)) {
				// Are we just editing the manifest, or are we loading a new one?
				if (!BaseManifest.Equals(ManifestName)) {
					Log.LogError($"[generate smapi manifest] Unable to locate <BaseManifest> at {basePath}");
					return false;
				}

				obj = new();

			} else {
				try {
					obj = JObject.Parse(File.ReadAllText(basePath));

				} catch (Exception ex) {
					Log.LogError($"[generate smapi manifest] Unable to load <BaseManifest>.\n{ex}");
					return false;
				}
			}

		} else
			obj = new();

		// Set the EntryDll if it has not yet been set, based on the
		// project's AssemblyName.
		if (! obj.ContainsKey("EntryDll"))
			obj.Add("EntryDll", $"{AssemblyName}.dll");

		// Use <Name> if it was specified. If the manifest has no name at all,
		// then fall back to AssemblyName as a safe value.
		if (!string.IsNullOrEmpty(Name))
			obj["Name"] = Name;
		else if (!obj.ContainsKey("Name"))
			obj["Name"] = AssemblyName;

		// Use <Author> if it was specified.
		if (!string.IsNullOrEmpty(Author))
			obj["Author"] = Author;
		else if (!obj.ContainsKey("Author"))
			Log.LogWarning("[generate smapi manifest] No <Author> is set and \"Author\" is not present in existing manifest.");

		// Set our <Version>.
		obj["Version"] = Version;

		// Use <Description> if it was specified.
		if (!string.IsNullOrEmpty(Description))
			obj["Description"] = Description;
		else if (!obj.ContainsKey("Description"))
			Log.LogWarning("[generate smapi manifest] No <Description> is set and \"Description\" is not present in existing manifest.");

		// Use <UniqueId> if it was specified.
		if (!string.IsNullOrEmpty(UniqueId))
			obj["UniqueID"] = UniqueId;
		else if (!obj.ContainsKey("UniqueID")) {
			Log.LogError("[generate smapi manifest] No <UniqueId> specified and \"UniqueID\" is not present in existing manifest. UniqueId is required.");
			return false;
		}

		// Use <MinimumApiVersion> if it was specified.
		if (!string.IsNullOrEmpty(MinimumApiVersion))
			obj["MinimumApiVersion"] = MinimumApiVersion;
		else if (!obj.ContainsKey("MinimumApiVersion"))
			Log.LogWarning("[generate smapi manifest] No <MinimumApiVersion> is set and \"MinimumApiVersion\" is not present in existing manifest.");

		// Use <UpdateKeys> if it was specified. This is a bit special because
		// we want to allow a comma separated list of values.
		if (!string.IsNullOrEmpty(UpdateKeys)) {
			string[] keys = UpdateKeys.Split(',')
				.Select(s => s.Trim())
				.Where(s => ! string.IsNullOrEmpty(s))
				.ToArray();

			obj["UpdateKeys"] = new JArray(keys);

		} else if (!obj.ContainsKey("UpdateKeys"))
			Log.LogWarning("[generate smapi manifest] No <UpdateKeys> is set, and \"UpdateKeys\" is not present in existing manifest.");

		// We don't currently support any other values.

		// Now save it.
		string result = obj.ToString(Formatting.Indented);

		string filename = Path.Combine(ProjectDir, ManifestName);
		File.WriteAllText(filename, result);
		return true;
	}



}

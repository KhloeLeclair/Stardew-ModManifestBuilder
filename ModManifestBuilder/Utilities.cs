using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;

namespace Leclair.Stardew.ModManifestBuilder;

public enum LogLevel {
	Trace,
	Debug,
	Info,
	Warning,
	Error
}

public static class Utilities {

	public static bool ManifestWarningsAsErrors = false;

	public const string LOG_PREFIX = "[generate smapi manifest]";
	public const string ADD_REF_PREFIX = "[add smapi mod references]";

	/// <summary>
	/// Parse an UpdateKey using the same logic as SMAPI.
	/// </summary>
	/// <param name="input">The raw UpdateKey</param>
	/// <param name="result">The UpdateKey split into provider, id, and subkey.</param>
	/// <returns>Whether or not it was parsed successfully.</returns>
	public static bool TryParseUpdateKey(string? input, out (string, string, string?)? result) {
		if (string.IsNullOrWhiteSpace(input)) {
			result = null;
			return false;
		}

		string site = input!.Trim();
		string? id;
		string? subkey;

		int idx = input.IndexOf(':');
		if (idx == -1)
			id = null;
		else {
			id = site.Substring(idx + 1).TrimStart();
			site = site.Substring(0, idx).TrimEnd();

			if (string.IsNullOrEmpty(id))
				id = null;
		}

		if (id is not null) {
			idx = id.IndexOf('@');
			if (idx == -1)
				subkey = null;
			else {
				subkey = id.Substring(idx);
				id = id.Substring(0, idx).TrimEnd();
				if (string.IsNullOrEmpty(id))
					id = null;
			}
		} else
			subkey = null;

		if (string.IsNullOrEmpty(site) || id is null) {
			result = null;
			return false;
		}

		result = (site, id, subkey);
		return true;
	}

	/// <summary>
	/// Creates a relative path from one file or folder to another.
	/// </summary>
	/// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
	/// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
	/// <returns>The relative path from the start directory to the end path.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="fromPath"/> or <paramref name="toPath"/> is <c>null</c>.</exception>
	/// <exception cref="UriFormatException"></exception>
	/// <exception cref="InvalidOperationException"></exception>
	public static string GetRelativePath(string fromPath, string toPath) {
		if (string.IsNullOrEmpty(fromPath)) {
			throw new ArgumentNullException("fromPath");
		}

		if (string.IsNullOrEmpty(toPath)) {
			throw new ArgumentNullException("toPath");
		}

		Uri fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
		Uri toUri = new Uri(AppendDirectorySeparatorChar(toPath));

		if (fromUri.Scheme != toUri.Scheme) {
			return toPath;
		}

		Uri relativeUri = fromUri.MakeRelativeUri(toUri);
		string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

		if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)) {
			relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
		}

		return relativePath;
	}

	private static string AppendDirectorySeparatorChar(string path) {
		// Append a slash only if the path is a directory and does not have a slash.
		if (!Path.HasExtension(path) &&
			!path.EndsWith(Path.DirectorySeparatorChar.ToString())) {
			return path + Path.DirectorySeparatorChar;
		}

		return path;
	}

	[Pure]
	public static bool IsValidModId(string? modId) {
		if (string.IsNullOrWhiteSpace(modId))
			return false;

		return Regex.IsMatch(modId, "^[a-z0-9_.-]+$", RegexOptions.IgnoreCase);
	}

	public static bool TryParseAssemblyName(this string? input, out AssemblyName? result) {
		if (input is not null) {
			try {
				result = new AssemblyName(input);
				return true;
			} catch { }
		}

		result = null;
		return false;
	}

	public static bool TryParseEnum<TEnum>(this string? input, out TEnum result) where TEnum : struct {
		if (string.IsNullOrWhiteSpace(input)) {
			result = default;
			return false;
		}

		return Enum.TryParse<TEnum>(input!, true, out result);
	}

	public static bool TryParseBoolean(this string? input, out bool result) {
		if (input is null) {
			result = false;
			return false;
		}

		return bool.TryParse(input, out result);
	}

	public static void LogAddRef(this TaskLoggingHelper log, LogLevel level, string message, string? file = null, int line = 0, int col = 0) {
		message = $"{ADD_REF_PREFIX} {message}";

		Log(log, level, message, file, line, col);
	}


	public static void LogGen(this TaskLoggingHelper log, LogLevel level, string message, string? file = null, int line = 0, int col = 0) {
		message = $"{LOG_PREFIX} {message}";

		Log(log, level, message, file, line, col);
	}

	public static void Log(this TaskLoggingHelper log, LogLevel level, string message, string? file = null, int line=0, int col=0) {
		if (ManifestWarningsAsErrors && level == LogLevel.Warning)
			level = LogLevel.Error;

		if (level == LogLevel.Trace)
			log.LogMessage(null, null, null, file, line, col, 0, 0, MessageImportance.Low, message);
		if (level == LogLevel.Debug)
			log.LogMessage(null, null, null, file, line, col, 0, 0, MessageImportance.Normal, message);
		if (level == LogLevel.Info)
			log.LogMessage(null, null, null, file, line, col, 0, 0, MessageImportance.High, message);
		else if (level == LogLevel.Warning)
			log.LogWarning(null, null, null, file, line, col, 0, 0, message);
		else if (level == LogLevel.Error)
			log.LogError(null, null, null, file, line, col, 0, 0, message);
	}

	public static Lazy<TEnum> GetEnumReader<TEnum>(Func<string?> input, TEnum defaultValue, TEnum? errorValue = null, TaskLoggingHelper? log = null) where TEnum : struct {
		TEnum Reader() {
			string? value = input();

			if (string.IsNullOrWhiteSpace(value))
				return defaultValue;

			if (Enum.TryParse<TEnum>(value, true, out TEnum result))
				return result;

			log?.LogGen(LogLevel.Error, $"Unable to parse value '{value}' for type {typeof(TEnum).Name}");
			return errorValue ?? defaultValue;
		}

		return new Lazy<TEnum>(Reader);
	}

	public static string[] GetSMAPIAssemblies(string? gamePath) {
		string? path = string.IsNullOrEmpty(gamePath) ? null : Path.Combine(gamePath, "smapi-internal");
		if (string.IsNullOrEmpty(path) || !Directory.Exists(path!))
			return Array.Empty<string>();

		List<string> result = new();

		foreach(string file in Directory.EnumerateFiles(path!)) {
			string ext = Path.GetExtension(file);
			switch(ext) {
				case ".dll":
				case ".exe":
					break;
				default:
					continue;
			}

			string fname = Path.GetFileNameWithoutExtension(file);
			if (!string.IsNullOrWhiteSpace(fname))
				result.Add(fname);
		}

		return result.ToArray();
	}

	public static string[] GetGameAssemblies(string? gamePath) {
		return new string[] {
			"BmFont",
			"FAudio-CS",
			"GalaxyCSharp",
			"GalaxyCSharpGlue",
			"Lidgren.Network",
			"MonoGame.Framework",
			"SkiaSharp",
			"Stardew Valley",
			"StardewValley.GameData",
			"Steamworks.NET",
			"TextCopy",
			"xTile"
		};
	}

	public static Dictionary<string, (ModManifest, SemanticVersion)> DiscoverMods(string? modsPath) {
		Dictionary<string, (ModManifest, SemanticVersion)> result = new();
		if (string.IsNullOrEmpty(modsPath) || ! Directory.Exists(modsPath))
			return result;

		bool OnFound(string path, ModManifest manifest, SemanticVersion version) {
			if (!result.ContainsKey(manifest.UniqueID!))
				result[manifest.UniqueID!] = (manifest, version);

			// Return true to keep iterating.
			return true;
		}

		RecursivelyFindMods(modsPath!, OnFound, checkManifest: false);

		return result;
	}

	public delegate bool FoundModDelegate(string path, ModManifest manifest, SemanticVersion version);

	public static bool RecursivelyFindMods(string path, FoundModDelegate onFound, bool checkManifest = true) {

		if (checkManifest) {
			string manifestFile = Path.Combine(path, "manifest.json");

			// If there's a manifest here, consume it.
			if (File.Exists(manifestFile))
				return TryConsumeMod(path, manifestFile, onFound);
		}

		foreach(string dir in Directory.EnumerateDirectories(path)) {
			string name = Path.GetFileName(dir);

			// Skip directories starting with a "."
			if (name is null || name.StartsWith("."))
				continue;

			// If we return false, to stop iterating, then stop here.
			if (!RecursivelyFindMods(dir, onFound, checkManifest: true))
				return false;
		}

		return true;
	}

	private static bool TryConsumeMod(string path, string manifestFile, FoundModDelegate onFound) {
		ModManifest manifest;
		try {
			manifest = JsonConvert.DeserializeObject<ModManifest>(File.ReadAllText(manifestFile));
		} catch {
			return true;
		}

		if (manifest?.UniqueID is null)
			return true;

		if (!SemanticVersion.TryParse(manifest.Version, out var modVer) || modVer is null) {
			return true;
		}

		try {
			return onFound(path, manifest, modVer);
		} catch {
			return true;
		}
	}

	/// <summary>
	/// Read the version of SMAPI, the game, and all the mods that the project has
	/// references for. This also checks to ensure that references to SMAPI
	/// and all mods have their <c>&lt;Private&gt;</c> set correctly to avoid
	/// including them in the build output and generates errors otherwise.
	/// </summary>
	/// <param name="references">The array of project references to read from.</param>
	/// <param name="Log">A logging helper we can log messages to.</param>
	/// <returns>The parsed SMAPI version and a dictionary of mod references</returns>
	public static (SemanticVersion?, SemanticVersion?, Dictionary<string, (ModManifest, SemanticVersion, VersionBehavior?)>) ParseReferences(ITaskItem[]? references, string? gamePath, TaskLoggingHelper Log) {
		SemanticVersion? smapiVersion = null;
		SemanticVersion? gameVersion = null;
		Dictionary<string, (ModManifest, SemanticVersion, VersionBehavior?)> modReferences = new();

		if (references is not null) {
			string[] smapiAssemblies = GetSMAPIAssemblies(gamePath);
			string[] gameAssemblies = GetGameAssemblies(gamePath);

			foreach (var reference in references) {
				if (TryParseBoolean(reference.GetMetadata("SMAPIDependency_Exclude"), out bool excl) && excl) {
					Log.LogGen(LogLevel.Debug, $"Skipping reference '{reference.ItemSpec}' with dependency exclusion flag.");
					continue;
				}

				// Instead of checking the filename, get the assembly name from
				// FusionName to better check if we find SMAPI or not.
				TryParseAssemblyName(reference.GetMetadata("FusionName"), out AssemblyName? an);

				if (an?.Name is not null) {
					int match = -1;
					if (smapiAssemblies.Contains(an.Name))
						match = 0;
					else if (gameAssemblies.Contains(an.Name))
						match = 1;

					if (match != -1 && (!reference.GetMetadata("Private").TryParseBoolean(out bool isPrivate) || isPrivate)) {
						string source = match == 0 ? "SMAPI" : "the game";
						Log.LogGen(LogLevel.Warning, $"Reference to '{an.Name}', which is provided by {source}, does not have <Private> set to \"false\" and may be included in the build output.");
					}
				}

				// Is it the game?
				if (an?.Name == "Stardew Valley") {
					// If we already have a version, then this is weird.
					if (gameVersion != null) {
						Log.LogGen(LogLevel.Warning, "Project has more than one reference to Stardew Valley.");
						continue;
					}

					// For the sake of parity, read the version the same as
					// everything else. We only care about {Major}.{Minor}.{Patch} though.
					try {
						FileVersionInfo fv = FileVersionInfo.GetVersionInfo(reference.ItemSpec);
						gameVersion = new SemanticVersion(fv.ProductVersion);
					} catch (Exception ex) {
						Log.LogGen(LogLevel.Warning, $"Unable to parse game version normally. Using fall back method. Error: {ex}");
						gameVersion = new SemanticVersion(an.Version);
					}

					// Log a warning if we're building against a version of
					// the game with a release tag. This likely means we're
					// building against an alpha or beta release.
					if (!string.IsNullOrWhiteSpace(gameVersion.Prerelease))
						Log.LogGen(LogLevel.Warning, $"The referenced version of Stardew Valley ('{gameVersion}') is not a standard release.");

					// Check to ensure that the SMAPI reference is not <Private>.
					if (!reference.GetMetadata("Private").TryParseBoolean(out bool isPrivate) || isPrivate)
						Log.LogGen(LogLevel.Warning, "Reference to Stardew Valley does not have <Private> set to \"false\" and will be included in the build output.");

					continue;
				}

				// Is it SMAPI?
				if (an?.Name == "StardewModdingAPI") {
					// If we already have a version, then this is weird.
					if (smapiVersion != null) {
						Log.LogGen(LogLevel.Warning, "Project has more than one reference to StardewModdingAPI.");
						continue;
					}

					// We really want to get the full semver if we can, so that
					// releases like alphas keep their tags.
					try {
						FileVersionInfo fv = FileVersionInfo.GetVersionInfo(reference.ItemSpec);
						smapiVersion = new SemanticVersion(fv.ProductVersion);
					} catch (Exception ex) {
						Log.LogGen(LogLevel.Warning, $"Unable to parse SMAPI version normally. Using fall back method. Error: {ex}");
						smapiVersion = new SemanticVersion(an.Version);
					}

					// Log a warning if we're building against a version of
					// SMAPI with a release tag. This likely means we're
					// building against an alpha or beta release.
					if (!string.IsNullOrWhiteSpace(smapiVersion.Prerelease))
						Log.LogGen(LogLevel.Warning, $"The referenced version of SMAPI ('{smapiVersion}') is not a standard release.");

					// Check to ensure that the SMAPI reference is not <Private>.
					if (!reference.GetMetadata("Private").TryParseBoolean(out bool isPrivate) || isPrivate)
						Log.LogGen(LogLevel.Warning, "Reference to SMAPI does not have <Private> set to \"false\" and will be included in the build output.");

					continue;
				}

				// See if we have a mod manifest for the reference.
				string refPath = Path.GetDirectoryName(reference.ItemSpec);
				string? manifestFile = string.IsNullOrEmpty(refPath) ? null : Path.Combine(refPath, "manifest.json");
				if (manifestFile is null || !File.Exists(manifestFile)) {
					// Try falling back to a project.
					string projFile = reference.GetMetadata("MSBuildSourceProjectFile");
					if (File.Exists(projFile)) {
						string projPath = Path.GetDirectoryName(projFile);
						manifestFile = string.IsNullOrEmpty(projPath) ? null : Path.Combine(projPath, "manifest.json");
						if (manifestFile is null || !File.Exists(manifestFile))
							continue;
					} else
						continue;
				}

				// If we do, try to read it as an SMAPI manifest.
				ModManifest? manifest;
				try {
					manifest = JsonConvert.DeserializeObject<ModManifest>(File.ReadAllText(manifestFile));
				} catch {
					continue;
				}

				// Now, if the manifest has an EntryDll that matches the
				// reference, along with a valid UniqueId and a valid
				// version, track it.
				string refFile = Path.GetFileName(reference.ItemSpec);
				if (manifest?.EntryDll != null && manifest.EntryDll.Equals(refFile, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(manifest.UniqueID) && SemanticVersion.TryParse(manifest.Version, out var version)) {
					VersionBehavior? behavior = TryParseEnum(reference.GetMetadata("SMAPIDependency_VersionBehavior"), out VersionBehavior bhv)
						? bhv : null;

					if (!behavior.HasValue && TryParseEnum(reference.GetMetadata("VersionBehavior"), out bhv))
						behavior = bhv;

					Log.LogGen(LogLevel.Info, $"Found reference to mod '{manifest.UniqueID}' with version {version} (version behavior: {behavior}).");
					modReferences[manifest.UniqueID!] = (manifest, version!, behavior);

					// Check to ensure that the mod reference is not <Private>.
					if (!reference.GetMetadata("Private").TryParseBoolean(out bool isPrivate) || isPrivate)
						Log.LogGen(LogLevel.Warning, $"Reference to mod '{manifest.UniqueID}' does not have <Private> set to \"false\" and will be included in the build output.");
				}
			}
		}

		return (smapiVersion, gameVersion, modReferences);
	}

}

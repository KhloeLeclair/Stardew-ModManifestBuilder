using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
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

	public static bool TryParseBoolean(this string? input, out bool result) {
		if (input is null) {
			result = false;
			return false;
		}

		return bool.TryParse(input, out result);
	}

	public static void LogAddRef(this TaskLoggingHelper log, LogLevel level, string message, params object[] messageArgs) {
		message = $"{ADD_REF_PREFIX} {message}";

		if (level == LogLevel.Trace)
			log.LogMessage(MessageImportance.Low, message, messageArgs);
		if (level == LogLevel.Debug)
			log.LogMessage(MessageImportance.Normal, message, messageArgs);
		if (level == LogLevel.Info)
			log.LogMessage(MessageImportance.High, message, messageArgs);
		else if (level == LogLevel.Warning)
			log.LogWarning(message, messageArgs);
		else if (level == LogLevel.Error)
			log.LogError(message, messageArgs);
	}

	public static void Log(this TaskLoggingHelper log, LogLevel level, string message, params object[] messageArgs) {
		message = $"{LOG_PREFIX} {message}";

		if (level == LogLevel.Trace)
			log.LogMessage(MessageImportance.Low, message, messageArgs);
		if (level == LogLevel.Debug)
			log.LogMessage(MessageImportance.Normal, message, messageArgs);
		if (level == LogLevel.Info)
			log.LogMessage(MessageImportance.High, message, messageArgs);
		else if (level == LogLevel.Warning)
			log.LogWarning(message, messageArgs);
		else if (level == LogLevel.Error)
			log.LogError(message, messageArgs);
	}

	public static Lazy<TEnum> GetEnumReader<TEnum>(Func<string?> input, TEnum defaultValue, TEnum? errorValue = null, TaskLoggingHelper? log = null) where TEnum : struct {
		TEnum Reader() {
			string? value = input();

			if (string.IsNullOrWhiteSpace(value))
				return defaultValue;

			if (Enum.TryParse<TEnum>(value, true, out TEnum result))
				return result;

			log?.Log(LogLevel.Error, $"Unable to parse value '{value}' for type {typeof(TEnum).Name}");
			return errorValue ?? defaultValue;
		}

		return new Lazy<TEnum>(Reader);
	}

	/// <summary>
	/// Read the version of SMAPI, as well as all the mods that the project has
	/// references for. This also checks to ensure that references to SMAPI
	/// and all mods have their <c>&lt;Private&gt;</c> set correctly to avoid
	/// including them in the build output and generates errors otherwise.
	/// </summary>
	/// <param name="references">The array of project references to read from.</param>
	/// <param name="Log">A logging helper we can log messages to.</param>
	/// <returns>The parsed SMAPI version and a dictionary of mod references</returns>
	public static (SemanticVersion?, Dictionary<string, (ModManifest, SemanticVersion)>) ParseReferences(ITaskItem[]? references, TaskLoggingHelper Log) {
		SemanticVersion? smapiVersion = null;
		Dictionary<string, (ModManifest, SemanticVersion)> modReferences = new();

		if (references is not null)
			foreach (var reference in references) {
				// Instead of checking the filename, get the assembly name from
				// FusionName to better check if we find SMAPI or not.
				reference
					.GetMetadata("FusionName")
					.TryParseAssemblyName(out AssemblyName? an);

				// Is it SMAPI?
				if (an?.Name == "StardewModdingAPI") {
					// If we already have a version, then this is weird.
					if (smapiVersion != null) {
						Log.Log(LogLevel.Warning, "Project has more than one reference to StardewModdingAPI.");
						continue;
					}

					// We really want to get the full semver if we can, so that
					// releases like alphas keep their tags.
					try {
						FileVersionInfo fv = FileVersionInfo.GetVersionInfo(reference.ItemSpec);
						smapiVersion = new SemanticVersion(fv.ProductVersion);
					} catch(Exception ex) {
						Log.Log(LogLevel.Warning, $"Unable to parse SMAPI version normally. Using fall back method. Error: {ex}");
						smapiVersion = new SemanticVersion(an.Version);
					}

					// Log a warning if we're building against a version of
					// SMAPI with a release tag. This likely means we're
					// building against an alpha or beta release.
					if (!string.IsNullOrWhiteSpace(smapiVersion.Release))
						Log.Log(LogLevel.Warning, $"The referenced version of SMAPI ('{smapiVersion}') is not a standard release.");

					// Check to ensure that the SMAPI reference is not <Private>.
					if (! reference.GetMetadata("Private").TryParseBoolean(out bool isPrivate) || isPrivate)
						Log.Log(LogLevel.Warning, "Reference to SMAPI does not have <Private> set to \"false\" and will be included in the build output.");

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
					Log.Log(LogLevel.Debug, $"Found reference to mod '{manifest.UniqueID}' with version {version}.");
					modReferences[manifest.UniqueID!] = (manifest, version!);

					// Check to ensure that the mod reference is not <Private>.
					if (!reference.GetMetadata("Private").TryParseBoolean(out bool isPrivate) || isPrivate)
						Log.Log(LogLevel.Warning, $"Reference to mod '{manifest.UniqueID}' does not have <Private> set to \"false\" and will be included in the build output.");
				}
			}

		return (smapiVersion, modReferences);
	}

}

using System;
using System.Collections.Generic;
using System.Text;

namespace Leclair.Stardew.ModManifestBuilder {

	public class SemanticVersion {

		public int Major { get; }

		public int Minor { get; }

		public int Patch { get; }

		public int Revision { get; }

		public string? Prerelease { get; set; }

		public string? Build { get; }

		public SemanticVersion(int major, int minor, int patch, int revision, string? release, string? build) {
			Major = major;
			Minor = minor;
			Patch = patch;
			Revision = revision;
			Prerelease = release;
			Build = build;
		}

		public SemanticVersion(Version version) {
			Major = version.Major;
			Minor = version.Minor;
			Patch = version.Build;
			Revision = version.Revision;
		}

		public SemanticVersion(string input) {

			if (!TryReadVersion(input, out int major, out int minor, out int patch, out int revision, out string? release, out string? build))
				throw new ArgumentException($"Cannot parse input '{input}' as a semantic version.");

			Major = major;
			Minor = minor;
			Patch = patch;
			Revision = revision;
			Prerelease = release;
			Build = build;
		}

		public int CompareTo(SemanticVersion? other, bool onlyMajorMinor = false, bool skipPrerelease = false) {
			return other == null
				? 1
				: CompareTo(other.Major, other.Minor, other.Patch, other.Revision, other.Prerelease, other.Build, onlyMajorMinor, skipPrerelease);
		}

		public bool Equals(SemanticVersion? other) {
			return other != null && CompareTo(other) == 0;
		}

		public bool IsOlderThan(SemanticVersion? other, bool onlyMajorMinor = false, bool skipPrerelease = false) {
			return CompareTo(other, onlyMajorMinor, skipPrerelease) < 0;
		}

		public bool IsNewerThan(SemanticVersion? other, bool onlyMajorMinor = false, bool skipPrerelease = false) {
			return CompareTo(other, onlyMajorMinor, skipPrerelease) > 0;
		}

		public bool IsBetween(SemanticVersion? min, SemanticVersion? max, bool onlyMajorMinor = false, bool skipPrerelease = false) {
			return CompareTo(min, onlyMajorMinor, skipPrerelease) >= 0 && CompareTo(max, onlyMajorMinor, skipPrerelease) <= 0;
		}

		public string ToShortString() {
			if (string.IsNullOrEmpty(Prerelease))
				return $"{Major}.{Minor}";
			return ToString();
		}

		public string ToNoPrereleaseString() {
			string version = $"{Major}.{Minor}.{Patch}";
			if (Revision != 0)
				version += $".{Revision}";
			return version;
		}

		public override string ToString() {
			string version = $"{Major}.{Minor}.{Patch}";
			if (Revision != 0)
				version += $".{Revision}";
			if (!string.IsNullOrEmpty(Prerelease))
				version += $"-{Prerelease}";
			if (!string.IsNullOrEmpty(Build))
				version += $"+{Build}";
			return version;
		}

		public static bool TryParse(string? input, out SemanticVersion? result) {
			if (TryReadVersion(input, out int major, out int minor, out int patch, out int revision, out string? release, out string? build)) {
				result = new SemanticVersion(
					major, minor, patch, revision, release, build
				);
				return true;
			}

			result = null;
			return false;
		}

		public int CompareTo(int major, int minor, int patch, int revision, string? release, string? build, bool onlyMajorMinor = false, bool skipPrerelease = false) {
			int result = CompareToInternal(major, minor, patch, revision, release, build, onlyMajorMinor, skipPrerelease);
			if (result < 0)
				return -1;
			if (result > 0)
				return 1;
			return 0;
		}

		private int CompareToInternal(int major, int minor, int patch, int revision, string? prerelease, string? build, bool onlyMajorMinor, bool skipPrerelease) {
			if (onlyMajorMinor && (!string.IsNullOrWhiteSpace(Prerelease) || !string.IsNullOrWhiteSpace(prerelease)))
				onlyMajorMinor = false;

			if (Major != major)
				return Major.CompareTo(major);
			if (Minor != minor)
				return Minor.CompareTo(minor);
			if (Patch != patch && !onlyMajorMinor)
				return Patch.CompareTo(patch);
			if (Revision != revision && !onlyMajorMinor)
				return Revision.CompareTo(revision);

			if (Prerelease == prerelease || skipPrerelease)
				return 0;

			if (string.IsNullOrWhiteSpace(Prerelease))
				return 1;
			if (string.IsNullOrWhiteSpace(prerelease))
				return -1;

			string[] parts = Prerelease?.Split('.', '-') ?? Array.Empty<string>();
			string[] other = prerelease?.Split('.', '-') ?? Array.Empty<string>();

			if (parts.Length < other.Length)
				return -1;
			if (parts.Length > other.Length)
				return 1;

			int length = parts.Length;
			for(int i = 0; i < length; i++) {

				string ours = parts[i];
				string theirs = other[i];

				if (ours.Equals(theirs, StringComparison.OrdinalIgnoreCase)) { 
					if (i == length - 1)
						return 0;
					continue;
				}

				if (theirs.Equals("unofficial", StringComparison.OrdinalIgnoreCase))
					return 1;
				if (ours.Equals("unofficial", StringComparison.OrdinalIgnoreCase))
					return -1;

				if (int.TryParse(ours, out int ourInt) && int.TryParse(theirs, out int theirInt))
					return ourInt.CompareTo(theirInt);

				return string.Compare(ours, theirs, StringComparison.OrdinalIgnoreCase);
			}

			// ???
			return 0;
		}

		public static bool TryReadVersion(string? input, out int major, out int minor, out int patch, out int revision, out string? release, out string? build) {
			major = 0;
			minor = 0;
			patch = 0;
			revision = 0;
			release = null;
			build = null;

			if (string.IsNullOrWhiteSpace(input))
				return false;

			ReadOnlySpan<char> raw = input.AsSpan();

			// Skip initial whitespace
			int index = 0;
			while (index < raw.Length && char.IsWhiteSpace(raw[index]))
				index++;

			// major.minor
			if (!TryReadNumber(raw, ref index, out major) || !TryReadLiteral(raw, ref index, '.') || !TryReadNumber(raw, ref index, out minor))
				return false;

			// patch is optional
			if (TryReadLiteral(raw, ref index, '.') && !TryReadNumber(raw, ref index, out patch))
				return false;

			// revision is optional
			if (TryReadLiteral(raw, ref index, '.') && !TryReadNumber(raw, ref index, out revision))
				return false;

			// release tag
			if (TryReadLiteral(raw, ref index, '-') && !TryReadTag(raw, ref index, out release))
				return false;

			// build tag
			if (TryReadLiteral(raw, ref index, '+') && !TryReadTag(raw, ref index, out build))
				return false;

			// Skip trailing whitespace.
			while (index < raw.Length && char.IsWhiteSpace(raw[index]))
				index++;

			// Ensure we consumed the entire input.
			return index == raw.Length;
		}


		private static bool TryReadLiteral(ReadOnlySpan<char> input, ref int index, char character) {
			if (index >= input.Length || input[index] != character)
				return false;

			index++;
			return true;
		}

		private static bool TryReadNumber(ReadOnlySpan<char> input, ref int index, out int value) {
			// Count the number of digit characters at this point in the input.
			int length = 0;
			while (length + index < input.Length && char.IsDigit(input[length + index]))
				length++;

			if (length == 0) {
				value = 0;
				return false;
			}

			// Leading zeroes are not allowed by semver.
			if (length > 1 && input[index] == '0') {
				value = 0;
				return false;
			}

			// *looks longingly towards .NET Core*
			string segment = input.Slice(index, length).ToString();

			if (int.TryParse(segment, out value)) {
				index += length;
				return true;
			}

			return false;
		}

		private static bool TryReadTag(ReadOnlySpan<char> input, ref int index, out string? value) {
			int length = 0;
			for (int i = index; i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '-' || input[i] == '.'); i++)
				length++;

			if (length == 0) {
				value = null;
				return false;
			}

			value = input.Slice(index, length).ToString();
			index += length;
			return true;
		}

	}
}

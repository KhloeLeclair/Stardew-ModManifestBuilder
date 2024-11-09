using System.Collections.Generic;

using Newtonsoft.Json;

namespace Leclair.Stardew.ModManifestBuilder;

public class ModManifest {

	[JsonProperty("$schema")]
	public string? JsonSchema { get; set; }

	[JsonProperty("$comment")]
	public object? JsonComment { get; set; }

	public string? UniqueID { get; set; }

	public string? Name { get; set; }

	public string? Author { get; set; }

	public string? Version { get; set; }

	public string? Description { get; set; }

	public string? MinimumApiVersion { get; set; }

	public string? MinimumGameVersion { get; set; }

	public string? EntryDll { get; set; }

	public ModContentPackFor? ContentPackFor { get; set; }

	public ModDependency[]? Dependencies { get; set; }

	public string[]? UpdateKeys { get; set; }

	[JsonExtensionData]
	public IDictionary<string, object> ExtraFields { get; set; } = new Dictionary<string, object>();

}

public class ModContentPackFor {

	public string? UniqueID { get; set; }

	public string? MinimumVersion { get; set; }

	[JsonExtensionData]
	public IDictionary<string, object> ExtraFields { get; set; } = new Dictionary<string, object>();
}

public class ModDependency {

	public string? UniqueID { get; set; }

	public string? MinimumVersion { get; set; }

	public bool? IsRequired { get; set; }

	[JsonExtensionData]
	public IDictionary<string, object> ExtraFields { get; set; } = new Dictionary<string, object>();
}

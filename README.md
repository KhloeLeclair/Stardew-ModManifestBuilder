**ModManifestBuilder** automatically updates your [SMAPI's manifest
file](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest) using
your project's properties.

## Contents

* [Why does this exist?](#why-does-this-exist)
* [Usage](#usage)
* [Available Properties](#available-properties)
* [See Also](#see-also)

## Why does this exist?

There's already a `<Version>` property in C# project files, which is used for
the built assembly's version, but this is a separate value than manifest and
needs to be set separately. Most people just ignore it.

By using this package, you can use your C# project file as a source of truth
for your mod's version. Your built assemblies will have their version set.

## Usage

1. [Install the NuGet package.](https://www.nuget.org/packages/Leclair.Stardew.ModManifestBuilder).
2. Set the appropriate `<Version>` in your mod's `.csproj` file.
3. Optionally, set other supported properties in your `.csproj` file.

Going forward, your `manifest.json` file should be updated automatically every
time you rebuild your project.

## Available Properties

<table>
<tr>
<th>Property</th>
<th>Description</th>
</tr>
<tr>
<th colspan=2>Manifest Properties</th>
</tr>
<tr>
<td><code>&lt;Name</code></td>
<td>

Name is saved to your manifest. If no `<Name>` is specified and your
manifest does not have an existing `"Name"`, then the project's
assembly name will be used as a default value.

</td>
</tr>
<tr>
<td><code>&lt;UniqueId&gt;</code></td>
<td>

UniqueId is saved to your manifest. This is a required field, and if not
present and not already included in your manifest, the build process
will fail.

</td>
</tr>
<tr>
<td><code>&lt;Author&gt;</code></td>
<td>Author is saved to your manifest. It has no special handling.</td>
</tr>
<tr>
<td><code>&lt;Description&gt;</code></td>
<td>Description is saved to your manifest. It has no special handling.</td>
</tr>
<tr>
<td><code>&lt;MinimumApiVersion&gt;</code></td>
<td>MinimumApiVersion is saved to your manifest. It has no special handling.</td>
</tr>
<tr>
<td><code>&lt;UpdateKeys&gt;</code></td>
<td>

UpdateKeys is saved to your manifest. It should be a comma-separated list of
update keys. See [the update checks documentation](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Update_checks)
for more details on update keys.

</td>
</tr>
<tr>
<td><code>&lt;Version&gt;</code></td>
<td>

Version is saved to your manifest. It should be a valid semver, otherwise
SMAPI will complain.

> Note: When using a non-Release configuration, the configuration will be
> appended to your version's prerelease. To avoid this, you can use the
> `<Version_AppendConfiguration>` flag.

</td>
</tr>
<tr>
<th colspan=2>Non-Manifest Properties</th>
</tr>
<tr>
<td><code>&lt;BaseManifest&gt;</code></td>
<td>

If BaseManifest is set, the generated manifest will use default values from
a file with that name in the project directory. You can use this to load
manifest values from a separate file to be saved into your manifest.

If this isn't set, the default values will be loaded from
`<ManifestName>` instead.

</td>
</tr>
<tr>
<td><code>&lt;ManifestName&gt;</code></td>
<td>

The generated manifest will be saved to a file with this name in the project
directory. By default, this is `manifest.json`

</td>
</tr>
<tr>
<td><code>&lt;Version_AppendConfiguration&gt;</code></td>
<td>

When enabled, and using a non-Release configuration to build, that configuration
will be appended to `<Version>`'s prerelease segment. Set to `false` to suppress
this behavior.

Default: `true`

</td>
</table>

## See Also

* [Changelog](CHANGELOG.md)

## Development

Actually working on this is awful. If anyone knows how to make MSBUILD run tasks
from a project dependency, please let me know. I tried everything that made sense
to me and nothing quite worked.

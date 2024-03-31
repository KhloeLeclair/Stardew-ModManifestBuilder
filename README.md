**ModManifestBuilder** automatically updates your [SMAPI's manifest
file](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest) using
your project's properties.


## Contents

* [Why does this exist?](#why-does-this-exist)
* [Why should I use this?](#why-should-i-use-this)
* [Usage](#usage)
* [Available Properties](#available-properties)
* [Dependencies](#dependencies)
* [See Also](#see-also)


## Why does this exist?

Initially, this project was created so that your manifest would remain in
sync with the `<Version>` property in C# project files. That way, the
built assemblies would get versions that match the manifest they're
shipped with.

Now, this project handles considerably more. You can create an entire manifest
using nothing but values from your C# project file. Every single manifest
value can be set, and most have a form of validation.


## Why should I use this?

There are several reasons you should use ModManifestBuilder:

1. Your project file becomes the sole source of truth. For existing projects,
   you have some values defined in your project file but you also need to
   define values, a few of them identical, in the manifest. This takes care
   of that for you.

   This is especially convenient if you develop using a single solution for
   many mods, as you can put several properties in a shared project file and
   have all your mods inherit them.

2. No more releasing debug builds by accident. Unless you disable the behavior,
   ModManifestBuilder will append your build configuration to the version of
   your mod for anything *other* than `Release`. This makes it easy to tell
   that something's off when your `.zip` file has `-Debug` at the end of
   the version.

3. ModManifestBuilder has validations that are applied to every field of your
   mod's manifest. Of note, `MinimumApiVersion` is compared to the version of
   SMAPI you're building against and, by default, you'll receive a warning if
   it's set to a previous version.

   ModManifestBuilder also checks for mods that you have references to, and
   it'll check that the mods are declared in your dependencies, that the
   `MinimumVersion` is set, and that the dependency is set to required.

4. You shouldn't use references if you can avoid it. Using APIs is best. But,
   if you *need* to use a reference, ModManifestBuilder makes it easier to
   add references as is documented below.


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
<td><code>&lt;Name&gt;</code></td>
<td>

Name is saved to your manifest to the `"Name"` field.

If no `<Name>` is specified and the loaded default values do not include a
value for `"Name"`, then the project's `AssemblyName` will be used as a
default value.

</td>
</tr>
<tr>
<td><code>&lt;Authors&gt;</code></td>
<td>

Authors is saved to your manifest to the `"Author"` field.

This field has no special handling or validation.

*Changed in 2.0:*
- Now using the standard `<Authors>`, instead of `<Author>`. Using `<Author>`
  will log a deprecation warning. Support for `<Author>` will be removed in 3.0

</td>
</tr>
<tr>
<td><code>&lt;Description&gt;</code></td>
<td>

Description is saved to your manifest to the `"Description"` field.

This field has no special handling or validation.

</td>
</tr>
<tr>
<td><code>&lt;UniqueId&gt;</code></td>
<td>

UniqueId is saved to your manifest to the `"UniqueID"` field.

This is a required field. If no `<UniqueId>` is specified, and the loaded
default values do not include a value for `"UniqueID"`, then ModManifestBuilder
will return an error and your project will fail to build.

Additionally, UniqueId can only contain the characters `A-Z`, `0-9`, `_`, `-`,
and `.`. If the UniqueId contains invalid characters, then ModManifestBuilder
will return an error and your project will fail to build.

*Changed in 2.0:*
- Added character validation.

</td>
</tr>
<tr>
<td><code>&lt;MinimumApiVersion&gt;</code></td>
<td>

MinimumApiVersion is saved to your manifest to the `"MinimumApiVersion"` field.

Depending on the value of `<MinimumApiVersion_Behavior>`, MinimumApiVersion will
be handled differently.

If the behavior is set to `Update` or `UpdateFull`, the input for
MinimumApiVersion will be ignored and the generated manifest's
`"MinimumApiVersion"` field will be set to the version of SMAPI that you're
building against.

`UpdateFull` will use the full version string, while `Update` will use
the format: `{MajorVersion}.{MinorVersion}`

> Note: If you're compiling against a non-release build of SMAPI, such as an
> alpha, then `Update` will still use the full version string.

If the behavior is set to `Ignore`, no validation will be performed. Otherwise,
the MinimumApiVersion is first validated as a valid semantic version. Then, the
version is compared to the version of SMAPI that your project is being built
against.

If the MinimumApiVersion is *older* than the installed version of SMAPI, a
warning will be logged. If the behavior is set to `Error`, then an error will
be logged instead and your project will fail to build.

*Changed in 2.0:*
- Added configurable behaviors, including validation, through the use of
  `<MinimumApiVersion_Behavior>`.

</td>
</tr>
<tr>
<td><code>&lt;UpdateKeys&gt;</code></td>
<td>

UpdateKeys is saved to your manifest to the `"UpdateKeys"` field.

UpdateKeys should be a comma-separated or semi-colon separated list of update
keys, and UpdateKeys should contain at least one value.

Each individual update key is validated to ensure it has a valid provider, and
that the mod ID is formatted correctly for that provider.

See [the update checks documentation](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Update_checks)
for more details on update keys.

*Changed in 2.0:*
- Added support for semi-colon separated lists to fit the way MSBuild handles
  lists normally.
- Added validation for update keys.

</td>
</tr>
<tr>
<td><code>&lt;Version&gt;</code></td>
<td>

Version is saved to your manifest to the "Version" field.

If this value is not a valid semantic version, ModManifestBuilder will return
an error and your project will not build.

> If your build configuration is not set to Release, the configuration will be
> appended to the version's pre-release. To disable this behavior, you can use
> the `<Version_AppendConfiguration>` flag.

</td>
</tr>
<tr>
<th colspan=2>Non-Manifest Properties</th>
</tr>
<tr>
<td><code>&lt;AlwaysSetEntryDll&gt;</code></td>
<td>

When this is enabled, `EntryDll` will always be overwritten in the generated
manifest using the pattern `{AssemblyName}.dll`

When this is not enabled, `EntryDll` will only be set in this manner when the
manifest does not already contain a value for `EntryDll`.

Default: `true`

*Added in 2.0*

</td>
</tr>
<tr>
<td><code>&lt;BaseManifest&gt;</code></td>
<td>

If BaseManifest is set, the generated manifest will use default values from
a file with that name in the project directory. You can use this to load
manifest values from a separate file to be saved into your manifest.

If this isn't set, the default values will be loaded from
`<ManifestName>` instead.

If this is set to the special value `new`, no default values will be loaded
and the manifest will be built from scratch.

*Changed in 2.0:*
- Added special value `new`.

</td>
</tr>
<tr>
<td><code>&lt;Dependencies_AlwaysIncludeRequire&gt;</code></td>
<td>

When this is enabled, dependencies in the generated manifest will have
their `IsRequired` values included and set to `true` when a dependency
is required.

As dependencies are considered required by default, we usually leave
out `IsRequired` when possible for brevity. 

> **Note:** If a dependency is optional, its `IsRequired` value will
> *always* be included, and set to `false`.

Default: `false`

*Added in 2.1*

</td>
</tr>
<tr>
<td><code>&lt;Dependencies_VersionBehavior&gt;</code></td>
<td>

This value controls how version checking is handled for
[Dependencies](#dependencies). It works similarly to
`<MinimumApiVersion_Behavior>`.

> At this time, dependency versions are *only* checked for mods that your
> project has a reference to or that have an associated `<SMAPIDependency />`
> entry within the project file.

When this value is `Update` or `UpdateFull` and the `"MinimumVersion"` field
of a dependency is an older version than the version of the mod your project
is being built against, the minimum version will be updated appropriately.

When this value is `Set`, `SetNoprerelease`, or `SetFull` and the `"MinimumVersion"`
field of a dependency is different than the version of the mod your project is
being built against, the minimum version will be set appropriately, even if
this means setting the minimum version to an older version.

When this value is `Warning` or `Error`, and the `"MinimumVersion"` field
of a dependency is an older version than the version of the mod your project
is being built against, a warning will be logged. If this value is `Error`, an
error will be logged instead and your project will fail to build.

Default: `UpdateFull`

*Added in 2.0*

*Changed in 2.2:*
- Added the `Set`, `SetFull`, and `SetNoPrerelease` values.

</td>
</tr>
<tr>
<td><code>&lt;ManifestComment&gt;</code></td>
<td>

When this is enabled, a comment will be included in the generated manifest file
indicating that the file is generated/updated automatically and that it should
not be modified directly.

If you expect to need to parse the manifest using a strict JSON parser that
does not allow comments, then this should be disabled.

SMAPI allows comments in JSON files, so leaving them enabled will not present
any issue using the resulting manifest with SMAPI.

Default: `true`

*Added in 2.1*

</td>
</tr>
<tr>
<td><code>&lt;ManifestName&gt;</code></td>
<td>

The generated manifest will be saved to a file with this name in the project
directory. By default, this is `manifest.json`

You should not change this unless you have build steps that copy the file to
`manifest.json` later for inclusion with your deployed mod.

</td>
</tr>
<tr>
<td><code>&lt;ManifestSchema&gt;</code></td>
<td>

When this is enabled, a `"$schema"` value will be included in the generated
manifest. By default, if enabled, the schema will be set to the standard
SMAPI manifest schema provided at: https://smapi.io/schemas/manifest.json

You can also set this to a custom URL, and the schema will be set to use
that URL instead.

> You may wish to use a custom schema if your manifest includes additional
> properties that are not part of the base manifest schema. While SMAPI
> allows manifests to include additional properties, the provided schema
> does not in an effort to protect developers from typos.

Default: `true`

*Added in 2.1*

</td>
</tr>
<tr>
<td><code>&lt;ManifestWarningsAsErrors&gt;</code></td>
<td>

When this is enabled, ModManifestBuilder will emit errors instead of warnings
for issues it finds with your project. This can be useful as errors prevent
a build from completing, thus forcing you to fix them before you can release
your project.

This can be used with a condition to only enable errors when building for
release by including a line like this in your project file:

```xml
<ManifestWarningsAsErrors Condition="$(Configuration) == 'Release'">true</ManifestWarningsAsErrors>
```

Default: `false`

*Added in 2.2*

</td>
</tr>
<tr>
<td><code>&lt;MinimumApiVersion_Behavior&gt;</close></td>
<td>

This value controls the behavior of `<MinimumApiVersion>`. See the relevant
section above for details on how this value functions.

Default: `Warning`

*Added in 2.0*

</td>
</tr>
<tr>
<td><code>&lt;References_VersionBehavior&gt;</code></td>
<td>

This optional value is similar to `<Dependencies_VersionBehavior>` but applies
specifically to mods that your mod references.

*Added in 2.1*

</td>
</tr>
<tr>
<td><code>&lt;Version_AppendConfiguration&gt;</code></td>
<td>

When this is enabled and your build configuration is not set to Release,
the configuration will be appended to `<Version>`'s pre-release.

Default: `true`

</td>
</table>


## Dependencies

Starting in version 2.0, ModManifestBuilder has support for managing the
`"Dependencies"` field of your manifest.


### Referenced Mods

ModManifestBuilder will automatically check every reference used by your
project for a valid SMAPI manifest. It does this by:

1. Iterating through every reference in
   `@(ReferencePathWithRefAssemblies);@(ReferenceDependencyPaths)`
2. Checking for a `manifest.json` file in the same directory as the reference.
3. Checking that the manifest's `EntryDll` is the same file as the reference.

Assuming the checks pass, ModManifestBuilder reads the `UniqueID` and `Version`
from the discovered manifest and ensures that:

1. Your project has a dependency for the mod with that `UniqueID`.
2. `IsRequired` is set to `true` for the dependency. As a reference, the other
   mod *is required* for your project to be able to load.
3. The `MinimumVersion` is set appropriately, as controlled by
   `<Dependencies_VersionBehavior>`.

> You can override the `VersionBehavior` of a specific reference by setting
> a `SMAPIDependency_VersionBehavior` on the reference.

As an example, assume you have a reference like this in your mod:

```xml
<Reference Include="DynamicGameAssets">
    <HintPath>$(GameModsPath)\DynamicGameAssets\DynamicGameAssets.dll</HintPath>
    <Private>false</Private>
    <SMAPIDependency_VersionBehavior>Update</SMAPIDependency_VersionBehavior>
</Reference>
```

ModManifestBuilder will end up finding the manifest at `$(GameModsPath)\DynamicGameAssets\manifest.json`,
ensure that its `EntryDll` is set to `DynamicGameAssets.dll`, and then ensure
that your manifest's `"Dependencies"` field has an entry similar to:

```json
{
    ...,
    "Dependencies": [
        ...,
        {
            "UniqueID": "spacechase0.DynamicGameAssets",
            "MinimumVersion": "1.4.4",
            "IsRequired": true
        }
    ]
}
```

> Note: If a referenced mod does not have `<Private>` set to `false`,
> ModManifestBuilder will log a warning. This is because a potentially
> private reference can be included in your project's output and you
> should never bundle another mod's DLLs in your own mod.


### Project References

Project references are automatically processed in the same way as
[referenced mods](#referenced-mods) with one difference regarding the discovery
of manifest files.

If a `manifest.json` file is not located alongside the reference's file, and
the reference has the metadata `MSBuildSourceProjectFile`, then we'll check
for a `manifest.json` file alongside the project file instead.

For example, if you have a mod project called `TestMod` and you compile it, you
might end up with a directory structure like this:

```
📁 MySolution/
   📁 TestMod/
      📁 bin/
         📁 Debug/
            📁 net5.0/
               🗎 TestMod.dll
      🗎 manifest.json
      🗎 ModEntry.cs
      🗎 TestMod.csproj
   📁 TestModTwo/
   🗎 MySolution.sln
```

After references have been resolved, your hypothetical project would have a
reference to the file `MySolution/TestMod/bin/Debug/net5.0/TestMod.dll` but
it's manifest isn't in that folder.

That reference would have `MSBuildSourceProjectFile` metadata with the path
to the file `MySolution/TestMod/TestMod.csproj` so we can check for a manifest
at that path as well, though we still check that `EntryDll` matches the name
of the reference itself.


### &lt;SMAPIDependency /&gt;

Finally, ModManifestBuilder adds a new tag that represents a mod that your
project depends on. `<SMAPIDependency>` tags should be added as children of
an `<ItemGroup>` in your project file.

The `<SMAPIDependency>` tag supports the following properties:

<table>
<tr>
<th>Property</th>
<th>Description</th>
</tr>
<tr>
<td><code>Include</code></td>
<td>

This is required, and should be set to the unique mod ID of the mod you
depend on.

</td>
</tr>
<tr>
<td><code>Version</code></td>
<td>

Optional string. This should be a valid semantic version representing the
minimum version of the mod that your project supports.

> Note: This may be set automatically if your project has a reference to
> this mod.

</td>
</tr>
<tr>
<td><code>VersionBehavior</code></td>
<td>

Optional enum value. Setting a `VersionBehavior` on a specific
`<SMAPIDependency>` tag allows you to customize the version behavior applied
for that specific dependency.

*Added in 2.1*

</td>
</tr>
<tr>
<td><code>Required</code></td>
<td>

Optional boolean. Whether or not this mod is *required* for your project to
function. This is saved to the `"IsRequired"` field of the dependency entry.

> Note: This *will* be forcibly set to `true` if your project has a reference
> to this mod.

</td>
</tr>
<tr>
<td><code>Reference</code></td>
<td>

Optional boolean. If this is set to true, a reference will be automatically
added to your project for this mod. This presents a slightly easier way to
add references to other mods.

If this is true, `Required` will be forcibly set to `true`.

If this is set to true and the required mod cannot be found, or if the required
mod is found but its version is older than the version in `Version`, then
an error will be logged and your project will fail to build.

</td>
</tr>
<tr>
<td><code>Assembly</code></td>
<td>

Optional string. This does nothing if `Reference` is not set to true. When
adding a reference to this mod, the assembly name is set to this value.

By default, this is set to the name of the mod's `EntryDll`, with the
file extension removed. This should only be set if the default behavior
is incorrect for a specific mod.

</td>
</tr>
</table>

As an example, the following dependency block will add a reference to the mod
SpaceCore and an optional dependency for the mod GenericModConfigMenu:

```xml
<ItemGroup>
    <SMAPIDependency Include="spacechase0.SpaceCore" Version="1.10" Reference="true" />
    <SMAPIDependency Include="spacechase0.GenericModConfigMenu" Version="1.9" Required="false" />
</ItemGroup>
```

That would result in the following entries being added to your manifest's
`"Dependencies"` field:

```json
{
    "Dependencies": [
        {
            "UniqueID": "spacechase0.GenericModConfigMenu",
            "MinimumVersion": "1.9",
            "IsRequired": false
        },
        {
            "UniqueID": "spacechase0.SpaceCore",
            "MinimumVersion": "1.10",
            "IsRequired": true
        }
    ]
}
```

The entry for SpaceCore is *also* roughly equivalent to adding the following
block to your project file:

```xml
<ItemGroup>
    <Reference Include="SpaceCore">
        <HintPath>$(GameModsPath)\SpaceCore\SpaceCore.dll</HintPath>
        <Private>false</Private>
    </Reference>
</ItemGroup>
```


## See Also

* [Changelog](CHANGELOG.md)


## Development

When working on this project, you'll likely find that you're unable to compile
ModManifestBuilder. This is because `msbuild` does not close immediately, and
it maintains locks on DLL files that tasks were loaded from.

In order to compile ModManifestBuilder, please first kill all running instances
of `msbuild`. If you're on windows, the following command works:

```
taskkill /f /im msbuild.exe
```

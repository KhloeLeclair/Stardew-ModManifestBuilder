## Changelog

### 2.4.1
Released on December 8th, 2024.

* Update dependencies to latest versions due to known vulnerabilities.
* Adjust the project file to hopefully improve the situation with
  dependencies in the future.
* Code cleanup.


### 2.4.0
Released on November 9th, 2024.

* Added support for ModBuildConfig's `<ContentPacks>` feature.
* Fixed the inclusion of build hashes when writing a version to a manifest.
* Fixed MSBuild trying to use the wrong version of our DLL (hopefully).


### 2.3.1
Released on April 8th, 2024.

* Fixed the game version parsing raising a warning when it cannot be parsed
  as a semantic version, as the game doesn't use semantic versioning.


### 2.3.0
Released on April 8th, 2024.

* Added `<MinimumGameVersion>` and `<MinimumGameVersion_Behavior>` for setting
  the new `"MinimumGameVersion"` manifest property.


### 2.2.0
Released on March 31st, 2024.

* Added `Set`, `SetNoPrerelease`, and `SetFull` as options for version behaviors,
  which are capable of setting a minimum version to an *older* version so long as
  you are building against an older version of a mod.
* Added the `<ManifestWarningsAsErrors>` property to allow you to receive errors
  rather than warnings for issues.
* Added a check for `GitHub` update keys that displays a warning when using an
  update subkey, as those are documented to not work correctly with `GitHub`.


### 2.1.0
Released on January 10th, 2023.

This was finished a while ago, but work and holidays pulled me away. Here's
the release, finally. This release adds more control over dependency and
reference version behavior.

* Added a check for references to game / SMAPI assemblies that do not have
  `Private` set appropriately, causing them to potentially be bundled with
  your build output. While Mod Build Config is likely to handle such
  references appropriately, it's still sloppy.
* Added the `<ManifestComment>` property to control whether or not a comment
  is included in generated manifest files.
* Added the `<ManifestSchema>` property to control the `"$schema"` property
  included in generated manifest files.
* Added the `<References_VersionBehavior>` property to control the version
  behavior for referenced mods specifically.
* Added the ability to set `VersionBehavior` metadata on `<SMAPIDependency />`
  tags to override the version behavior for specific dependencies.
* Added the ability to set `SMAPIDependency_VersionBehavior` metadata on
  other references to dependencies to override the version behavior for
  those dependencies.


### 2.0.1
Released on December 11th, 2022.

* Fixed an issue where the `.targets` file could not find the task DLL.
  (Apparently search paths work differently in release...?)


### 2.0.0
Released on December 11th, 2022.

* Added: Support for building dependencies, including the ability to inject
  references to mods.
* Added: Validation for most manifest fields.
* Added: Ability to automatically update the `"MinimumApiVersion"` field based
  on the version of SMAPI that is being built against.
* Changed: The `"EntryDll"` field is now always updated by default, unless
  `<AlwaysSetEntryDll>` is set to false.
* Changed: `<UpdateKeys>` now supports semi-colon separated values, in order to
  follow MSBuild conventions for a list.
* Changed: `<BaseManifest>` can now be set to `new` to disable loading previous or
  default values, instead building a manifest from scratch each time.


### 1.0.3
Released on March 31st, 2022.

* Fixing build issues.


### 1.0.2
Released on March 31st, 2022.

* Fixed: Use `UniqueID` in manifest files rather than `UniqueId`.


### 1.0.0
Released on March 31st, 2022.

Initial release.

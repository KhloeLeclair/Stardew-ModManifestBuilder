## Changelog

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

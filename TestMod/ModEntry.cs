using System;

using StardewModdingAPI;

using SpaceCore;

namespace TestMod;

public class ModEntry : Mod {

	public override void Entry(IModHelper helper) {
		Monitor.Log("Loaded.", LogLevel.Info);
	}

}

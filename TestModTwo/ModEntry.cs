using System;

using StardewModdingAPI;

namespace TestModTwo;

public class ModEntry : Mod {

	public override void Entry(IModHelper helper) {
		Monitor.Log("Loaded.", LogLevel.Info);
	}

}

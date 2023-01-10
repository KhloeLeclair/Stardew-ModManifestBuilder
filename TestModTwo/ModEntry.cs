using System;

using Newtonsoft.Json;

using StardewModdingAPI;

namespace TestModTwo;

public class ModEntry : Mod {

	public override void Entry(IModHelper helper) {
		Monitor.Log("Loaded.", LogLevel.Info);

		Monitor.Log(JsonConvert.SerializeObject(null), LogLevel.Info);

	}

}

using System;
using RoR2;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using ST = SuspiciousTentacle;
using MonoMod.RuntimeDetour;

namespace BodyBlend
{
	class SuspiciousTentacleCompatibility
	{
		private static bool? _enabled;

		public static bool enabled
		{
			get
			{
				if (_enabled == null)
				{
					_enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(SuspiciousTentacle.SuspiciousTentacle.PluginGUID);
				}
				return (bool)_enabled;
			}
		}

		// TODO: Have this be implemented by Suspicious Tentacle
		public static void HookGrowthProgress()
		{
			if (enabled)
			{
				new Hook(
					typeof(ST.SuspiciousTentacle).GetMethod(nameof(ST.SuspiciousTentacle.GrowthProgress)),
					typeof(SuspiciousTentacleCompatibility).GetMethod(nameof(OnGrowthProgress)));
			}
		}

		public static float OnGrowthProgress(
			Func<ST.SuspiciousTentacle, CharacterBody, float> orig,
			ST.SuspiciousTentacle self,
			CharacterBody body)
		{
			BodyBlendController controller = body.modelLocator.modelTransform.gameObject.GetComponent<BodyBlendController>();
			float value = orig(self, body);
			if (controller)
				controller.SetBlendTargetWeight("Belly", value, "SuspiciousTentacle");
			return value;
		}
	}
}

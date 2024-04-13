using MonoMod.RuntimeDetour;
using RoR2;
using System;
using ST = SuspiciousTentacle;

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
					_enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.RuneFoxMods.SuspiciousTentacle");
				}
				return (bool)_enabled;
			}
		}

		// TODO: Have this be implemented by Suspicious Tentacle
		public static void HookGrowthProgress()
		{
			new Hook(
				typeof(ST.SuspiciousTentacle).GetMethod(nameof(ST.SuspiciousTentacle.GrowthProgress)),
				typeof(SuspiciousTentacleCompatibility).GetMethod(nameof(OnGrowthProgress)));
		}

		public static float OnGrowthProgress(
			Func<ST.SuspiciousTentacle, CharacterBody, float> orig,
			ST.SuspiciousTentacle self,
			CharacterBody body)
		{
			float value = orig(self, body);
			if (BodyBlendOptions.EnableSusTentacleCompat.Value)
			{
				float count = body.inventory.GetItemCount(ST.SuspiciousTentacle.SusTentacleItemDef);
				BodyBlendController controller = body.modelLocator.modelTransform.gameObject.GetComponent<BodyBlendController>();
				if (controller)
				{
					foreach (var part in BodyBlendOptions.SusTentacleParts)
					{
						controller.SetBlendTargetWeight(part, UnityEngine.Mathf.Clamp01(value * count * BodyBlendOptions.SusTentacleSizePerEgg.Value), "SuspiciousTentacle");
					}
				}
			}
			return value;
		}
	}
}

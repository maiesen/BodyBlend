using BepInEx.Configuration;
using MonoMod.RuntimeDetour;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RiskOfOptions;
using RoR2;
using System;
using System.Collections.Generic;
using ST = SuspiciousTentacle;
using static AkMIDIEvent;
using UnityEngine;
using System.Linq;
using BepInEx.Bootstrap;
using SuspiciousTentacle;
using BepInEx;

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
					_enabled = Chainloader.PluginInfos.ContainsKey("com.RuneFoxMods.SuspiciousTentacle");
				}
				return (bool)_enabled;
			}
		}

		// TODO: Have this be implemented by Suspicious Tentacle

		private static BaseUnityPlugin SuspiciousTentaclePlugin;
		private static ConfigEntry<bool> EnableSusTentacleCompat;

		private static void FindPluginInstance()
		{
			foreach (var plugin in Chainloader.PluginInfos)
			{
				var metadata = plugin.Value.Metadata;
				if (metadata.GUID.Equals(ST.SuspiciousTentacle.PluginGUID))
				{
					SuspiciousTentaclePlugin = plugin.Value.Instance;
					break;
				}
			}
		}


		// Auto configuration for all loaded body parts.
		private static Dictionary<string, float> DefaultPartInfluences = new Dictionary<string, float>()
		{
			{ "Belly", 25f }
		};

		protected static Dictionary<string, ConfigEntry<float>> PartInfluences = new Dictionary<string, ConfigEntry<float>>();

		public static ICollection<string> GetParts()
		{
			return PartInfluences.Keys;
		}

		public static float GetInfluence(string part)
		{
			// Percentage -> Need to be divided by 100 always.
			return PartInfluences[part].Value / 100f;
		}

		public static void SetupCompatibility()
		{
			// Grab relevant data
			FindPluginInstance();
			// Add config under suspicious tentacle
			var config = SuspiciousTentaclePlugin.Config;

			// Set configs
			EnableSusTentacleCompat = config.Bind("BodyBlend", "Enable Compatibility", true, "Enable compatibility with BodyBlend.");
			ModSettingsManager.AddOption(
				new CheckBoxOption(EnableSusTentacleCompat, new CheckBoxConfig()),
					ST.SuspiciousTentacle.PluginGUID, ST.SuspiciousTentacle.PluginName
			);

			foreach (var item in DefaultPartInfluences)
			{
				CreatePartConfig(config, item.Key, item.Value);
			}
			foreach (var part in BodyBlendPlugin.GetBodyBlendParts())
			{
				if (PartInfluences.ContainsKey(part)) continue;
				CreatePartConfig(config, part, 0f);
			}
		}

		private static void CreatePartConfig(ConfigFile config, string part, float defaultInfluence)
		{
			var influenceConfig = config.Bind<float>(
					"BodyBlend",
					$"{part} Influence", defaultInfluence,
					$"Determine how much \"{part}\" will get influenced by Suspicious Tentacle per egg.\n" +
					$"Default: {defaultInfluence:0}"
				);
			ModSettingsManager.AddOption(
				new SliderOption(influenceConfig, new SliderConfig { min = 0f, max = 100f, checkIfDisabled = GetSusTentacleBodyBlendDisable }),
					ST.SuspiciousTentacle.PluginGUID, ST.SuspiciousTentacle.PluginName
			);

			PartInfluences[part] = influenceConfig;
		}

		public static void HookGrowthProgress()
		{
			On.RoR2.CharacterBody.Update += UpdateBodyBlend;
		}

		private static float GetBuffDuration(CharacterBody body, BuffIndex buffIndex)
		{
			var timedBuffs = body.timedBuffs;
			if (timedBuffs == null)
				return 0;
			if (timedBuffs.Count > 0)
			{
				return timedBuffs.Max(p =>
				{
					if (p.buffIndex == buffIndex)
					{
						return p.timer;
					}
					return 0;
				});
			}
			return 0;
		}

		protected static void UpdateBodyBlend(On.RoR2.CharacterBody.orig_Update orig, CharacterBody body)
		{
			orig(body);

			if (!EnableSusTentacleCompat.Value) return;

			var buffIdx = ((ST.SuspiciousTentacle) SuspiciousTentaclePlugin).EggGrowthDebuff.buffIndex;
			if (!body.HasBuff(buffIdx)) return;

			float progress = 0.0f;
			if (ST.SuspiciousTentacle.EggGrowthTime.Value > 0)
			{
				progress = 1.0f - (GetBuffDuration(body, buffIdx) / ST.SuspiciousTentacle.EggGrowthTime.Value);
			}
			float eggCount = body.inventory.GetItemCount(ST.SuspiciousTentacle.SusTentacleItemDef);
			if (eggCount < 1) eggCount = 1;

			BodyBlendController controller = body.modelLocator.modelTransform.gameObject.GetComponent<BodyBlendController>();
			if (!controller) return;

			foreach (var part in PartInfluences.Keys)
			{
				float influence = GetInfluence(part);
				if (influence > 0.0001f)
				{
					controller.SetBlendTargetWeight(part, progress * influence * eggCount, "SuspiciousTentacle");
				}
				else
				{
					controller.RemoveBlendTargetWeight(part, "SuspiciousTentacle");
				}
			}
		}

		private static bool GetSusTentacleBodyBlendDisable()
		{
			return !EnableSusTentacleCompat.Value;
		}
	}
}
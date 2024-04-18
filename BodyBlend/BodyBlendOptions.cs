using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BepInEx;
using BepInEx.Configuration;
using BodyBlend.Utils;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;
using static BodyBlend.Utils.BodyBlendUtils;

namespace BodyBlend
{
	internal static class BodyBlendOptions
	{
		internal static ConfigEntry<bool> UseLerp;
		internal static ConfigEntry<float> LerpSpeed;
		internal static ConfigEntry<float> BoneUpdateInterval;

		internal static NestedDictionary<string, string, ConfigEntry<float>> MinBlendConfigs = new NestedDictionary<string, string, ConfigEntry<float>>();
		internal static NestedDictionary<string, string, ConfigEntry<float>> MaxBlendConfigs = new NestedDictionary<string, string, ConfigEntry<float>>();
		internal static List<string> SusTentacleParts = new List<string>();

		internal static bool CheckUseLerp()
		{
			return !UseLerp.Value;
		}

		internal static void InitializeOptions(ConfigFile config)
		{
			UseLerp = config.Bind("BodyBlend", "Use Lerp", true, "Enable smooth transition. If disabled, the model instantly changes to match the target size.");
			ModSettingsManager.AddOption(
				new CheckBoxOption(UseLerp, new CheckBoxConfig())
			);
			LerpSpeed = config.Bind("BodyBlend", "Lerp Speed", 2.0f, "If lerp is enabled, this controls how fast the model changes to match the target size.\n" +
				"Default: 2.0");
			ModSettingsManager.AddOption(
				new StepSliderOption(LerpSpeed, new StepSliderConfig() { min = 0.1f, max = 10.0f, increment = 0.1f, checkIfDisabled = CheckUseLerp })
			);
			BoneUpdateInterval = config.Bind("BodyBlend", "Bone Update Interval", 0.15f, "Intervals between each update to dynamic bone.\n" +
				"Default: 0.15");
			ModSettingsManager.AddOption(
				new StepSliderOption(BoneUpdateInterval, new StepSliderConfig() { min = 0.01f, max = 1.0f, increment = 0.01f })
			);

			ModSettingsManager.AddOption(new GenericButtonOption("", "BodyBlend", 
				"Click to reload all BodyBlend configuration files in the /plugins folder.\n" +
				"Check the console log to see if files have been reloaded.\n" +
				"Any adjustment will only apply after entering a new stage.\n" +
				"If you have added a new part in the config file, you'll need to restart the game for BodyBlend to work properly.",
				"Reload Config Files", BodyBlendPlugin.ReloadJson));

			foreach (var control in RegisteredSkinBlendControls)
			{
				var skinToken = control.Key;

				var categoryName = GetSkinName(skinToken);
				
				var minConfigDict = new Dictionary<string, ConfigEntry<float>>();
				var maxConfigDict = new Dictionary<string, ConfigEntry<float>>();
				foreach (var partControl in control.Value)
				{
					var partName = partControl.Key;

					var minConfigEntry = config.Bind(categoryName, $"Min {partName} Size", 0f, $"Minimum size for the part \"{partName}\".\nBodyBlend will interpolate the size from the minimum value to the maximum value.");
					ModSettingsManager.AddOption(
						new SliderOption(minConfigEntry, new SliderConfig { min = 0f, max = 100f })
					);
					minConfigDict.Add(partName, minConfigEntry);

					var maxConfigEntry = config.Bind(categoryName, $"Max {partName} Size", 100f, $"Maximum size for the part \"{partName}\".\nBodyBlend will interpolate the size from the minimum value to the maximum value.");
					ModSettingsManager.AddOption(
						new SliderOption(maxConfigEntry, new SliderConfig { min = 0f, max = 100f })
					);
					maxConfigDict.Add(partName, maxConfigEntry);
				}
				MinBlendConfigs.Add(skinToken, minConfigDict);
				MaxBlendConfigs.Add(skinToken, maxConfigDict);
			}
		}
	}
}

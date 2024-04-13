using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BepInEx;
using BepInEx.Configuration;
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
		internal static ConfigEntry<bool> EnableSusTentacleCompat;
		internal static ConfigEntry<string> SusTentaclePartsConfig;
		internal static ConfigEntry<float> SusTentacleSizePerEgg;

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

			if (SuspiciousTentacleCompatibility.enabled)
			{
				EnableSusTentacleCompat = config.Bind("Suspicious Tentacle", "Enable Compatibility", true, "Enable compatibility with Suspicious Tentacle.");
				ModSettingsManager.AddOption(
					new CheckBoxOption(EnableSusTentacleCompat, new CheckBoxConfig())
				);

				SusTentaclePartsConfig = config.Bind<string>(
					"Suspicious Tentacle",
					"Affected Parts", "Belly",
					"Determine which body part will get influenced by BodyBlend. Set multiple parts by separating them with |.\n" +
					"Example: Belly|Breasts\n" +
					"Default: Belly"
				);
				ModSettingsManager.AddOption(
					new StringInputFieldOption(SusTentaclePartsConfig, new InputFieldConfig() { submitOn = InputFieldConfig.SubmitEnum.OnExitOrSubmit, checkIfDisabled = GetSusTentacleBodyBlendDisable })
				);
				SusTentaclePartsConfig.SettingChanged += OnPartsUpdated;
				SusTentacleSizePerEgg = config.Bind("Suspicious Tentacle", "Size per Egg", 0.2f, "What percentage of max size each egg contributes to.\n" +
					"Default: 0.2");
				ModSettingsManager.AddOption(
					new StepSliderOption(SusTentacleSizePerEgg, new StepSliderConfig() { min = 0.0f, max = 1.0f, increment = 0.01f, checkIfDisabled = GetSusTentacleBodyBlendDisable })
				);

				OnPartsUpdated(null, null);
			}

			foreach (var control in RegisteredSkinBlendControls)
			{
				var skinToken = control.Key;
				var configDict = new Dictionary<string, ConfigEntry<float>>();
				foreach (var partControl in control.Value)
				{
					var partName = partControl.Key;
					var configEntry = config.Bind(skinToken, partName, 100f, "Maximum size");
					ModSettingsManager.AddOption(
						new SliderOption(configEntry, new SliderConfig { min = 0f, max = 100f })
					);
					configDict.Add(partName, configEntry);
				}
				MaxBlendConfigs.Add(skinToken, configDict);
			}
		}

		private static bool GetSusTentacleBodyBlendDisable()
		{
			return !EnableSusTentacleCompat.Value;
		}

		private static void OnPartsUpdated(object _o, EventArgs _i)
		{
			SusTentacleParts.Clear();

			SusTentaclePartsConfig.Value.Split('|').ToList().ForEach(item => SusTentacleParts.Add(item.Trim()));
		}
	}
}

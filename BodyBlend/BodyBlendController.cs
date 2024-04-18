using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static BodyBlend.Utils.BodyBlendUtils;

namespace BodyBlend
{
	[DisallowMultipleComponent]
	public class BodyBlendController : MonoBehaviour
	{
		private DictionaryList<string, BodyBlendControl> bodyBlendControls = new DictionaryList<string, BodyBlendControl>();
		private string SkinNameToken = "";

		public void Awake()
		{

		}

		public void LateUpdate()
		{
			foreach (var item in bodyBlendControls)
			{
				var maxBlend = FetchMaxBlendConfig(item.Key);
				var minBlend = FetchMinBlendConfig(item.Key);
				foreach (var item2 in item.Value)
				{
					item2.SetMinBlend(minBlend);
					item2.SetMaxBlend(maxBlend);
					item2.Update(Time.deltaTime);
				}
			}
		}

		private float FetchMinBlendConfig(string name)
		{
			var value = BodyBlendOptions.MinBlendConfigs.GetValue(SkinNameToken, null)?.GetValue(name, null)?.Value;
			if (value == null) return 0.0f;
			return (float)value / 100f;
		}

		private float FetchMaxBlendConfig(string name)
		{
			var value = BodyBlendOptions.MaxBlendConfigs.GetValue(SkinNameToken, null)?.GetValue(name, null)?.Value;
			if (value == null) return 1.0f;
			return (float)value / 100f;
		}

		// TODO: Clarify the time and value of AnimationCurve and how it relate to setting weights of blendshapes
		// Set blend time?

		// Set the target blend weight for a given blend name and source. setInstant determines if the change happen instantly
		// Most mods should support weight value from 0f to 1f. Unexpected results may occur if weight value goes above 1f.
		// Source is required for handling multiple set weight requests
		public void SetBlendTargetWeight(string name, float value, string source = "Default", bool setInstant = false)
		{
			if (HasBlendControl(name))
			{
				foreach (var item in bodyBlendControls[name])
				{
					item.SetTargetWeight(value, source);
					if (setInstant)
						item.Update(0f, true);
				}
			}
		}

		// Set the target blend weight (percentage based) for a given blend name and source. setInstant determines if the change happen instantly
		// Most mods should support weight value from 0f to 1f.
		// The resulting weight will be calculated after the additive sources are summed up. The value scales from that sum to 1.0f.
		public void SetBlendTargetWeightPercent(string name, float value, string source = "Default", bool setInstant = false)
		{
			if (HasBlendControl(name))
			{
				foreach (var item in bodyBlendControls[name])
				{
					item.SetTargetWeightPercent(value, source);
					if (setInstant)
						item.Update(0f, true);
				}
			}
		}

		// For removing target weight so that it will no longer affect the calculation
		public void RemoveBlendTargetWeight(string name, string source = "Default")
		{
			if (HasBlendControl(name))
			{
				foreach (var item in bodyBlendControls[name])
					item.RemoveTargetWeight(source);
			}
		}

		public bool HasBlendControl(string name)
		{
			return bodyBlendControls.ContainsKey(name);
		}

		internal void AddBlendControl(string name, BodyBlendControl control)
		{
			if (!bodyBlendControls.ContainsKey(name))
				bodyBlendControls[name] = new List<BodyBlendControl>();
			bodyBlendControls[name].Add(control);
		}

		internal void RemoveBlendControl(string name)
		{
			bodyBlendControls.Remove(name);
		}

		internal void SetSkinNameToken(string name)
		{
			SkinNameToken = name;
		}
	}

	internal class BodyBlendControl
	{

		public Dictionary<int, AnimationCurve> blendShapeControls = new Dictionary<int, AnimationCurve>();

		public List<DynamicBoneControl> boneControls = new List<DynamicBoneControl>();

		private SkinnedMeshRenderer targetRenderer;

		private float minBlend = 0.0f;
		private float maxBlend = 1.0f;

		private float elapsedTime = 0f;
		private float currentWeight = 0f;
		// additive weights. ex: 0.2 and 0.4 from two sources will combine to 0.6
		private Dictionary<string, float> targetWeights = new Dictionary<string, float>() { { "Default", 0f } };
		// percentage based weights. will scale from sum of additive sources to 1.0. only max value will be used from this list
		private Dictionary<string, float> targetWeightPercents = new Dictionary<string, float>() { { "Default", 0f } };

		public BodyBlendControl(SkinnedMeshRenderer targetRenderer = null)
		{
			this.targetRenderer = targetRenderer;
		}

		public void SetTargetRenderer(SkinnedMeshRenderer rederer)
		{
			targetRenderer = rederer;
		}

		public void AddDynBoneControl(
			List<DynamicBone> bones,
			AnimationCurve inertCurve = null,
			AnimationCurve elasticityCurve = null,
			AnimationCurve stiffnessCurve = null,
			AnimationCurve dampingCurve = null,
			DynBoneControlMode mode = DynBoneControlMode.FULL_CONTROL)
		{
			var boneControl = new DynamicBoneControl();
			boneControl.SetBoneControls(bones, inertCurve, elasticityCurve, stiffnessCurve, dampingCurve, mode);

			boneControls.Add(boneControl);
		}

		public void SetTargetWeight(float weight, string source)
		{
			targetWeights[source] = weight;
		}

		public void SetTargetWeightPercent(float weight, string source)
		{
			targetWeightPercents[source] = weight;
		}

		public void RemoveTargetWeight(string source)
		{
			targetWeights.Remove(source);
		}

		public void SetMaxBlend(float value) { maxBlend = value; }

		public void SetMinBlend(float value) { minBlend = value; }

		public void Update(float t, bool forceNoLerp = false)
		{
			// Check if dynamic bones need updating
			elapsedTime += t;
			bool doBoneUpdate = false || forceNoLerp;
			if (BodyBlendOptions.BoneUpdateInterval.Value <= 0f)
			{
				doBoneUpdate = true;
			}
			else if (elapsedTime >= BodyBlendOptions.BoneUpdateInterval.Value)
			{
				elapsedTime -= BodyBlendOptions.BoneUpdateInterval.Value;
				elapsedTime = Mathf.Min(elapsedTime, BodyBlendOptions.BoneUpdateInterval.Value);
				doBoneUpdate = true;
			}

			if (BodyBlendOptions.UseLerp.Value && !forceNoLerp)
				currentWeight = Mathf.Lerp(currentWeight, GetTargetWeight(), t * BodyBlendOptions.LerpSpeed.Value);
			else
				currentWeight = GetTargetWeight();

			// Lerp currentWeight to configured min to max
			var displayWeight = Mathf.Lerp(minBlend, maxBlend, currentWeight);

			if (doBoneUpdate && boneControls != null)
			{
				foreach (var control in boneControls)
				{
					control.UpdateBoneValues(displayWeight);
				}
			}

			if (targetRenderer == null)
				return;

			// Update BlendShapes
			foreach (var item in blendShapeControls)
			{
				float value = item.Value.Evaluate(displayWeight);
				targetRenderer.SetBlendShapeWeight(item.Key, value * 100f);
			}
		}

		private float GetTargetWeight()
		{
			var additiveWeight = targetWeights.Values.Sum();
			additiveWeight = Mathf.Clamp01(additiveWeight);
			var percentageWeight = targetWeightPercents.Values.Max();
			return Mathf.Lerp(additiveWeight, 1.0f, percentageWeight);
		}
	}
}

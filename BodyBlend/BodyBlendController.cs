using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BodyBlend
{
	public enum WeightMode
	{
		AVERAGE,
		MINIMUM,
		MAXIMUM
	}

	public enum DynBoneControlMode
	{
		ZERO_TO_BASE,
		FULL_CONTROL,
		BASE_TO_ONE
	}

	[DisallowMultipleComponent]
	public class BodyBlendController : MonoBehaviour
	{
		private DictionaryList<string, BodyBlendControl> bodyBlendControls = new DictionaryList<string, BodyBlendControl>();

		class DictionaryList<K1, V> :
			Dictionary<K1, List<V>>
		{ }

		public void Awake()
		{

		}

		public void LateUpdate()
		{
			foreach (var item in bodyBlendControls.Values)
				foreach (var item2 in item)
					item2.Update(Time.deltaTime);
		}

		private float updateVal = 0.0f;
		private float updateVal2 = 0.0f;
		public void Update()
		{
			// TODO: Only for testing, remove in release build
			float dt = Time.deltaTime;
			if (Input.GetKey(KeyCode.Period))
			{
				updateVal += dt;
			}
			if (Input.GetKey(KeyCode.Comma))
			{
				updateVal -= dt;
			}
			updateVal = Mathf.Clamp01(updateVal);

			if (Input.GetKey(KeyCode.L))
			{
				updateVal2 += dt;
			}
			if (Input.GetKey(KeyCode.K))
			{
				updateVal2 -= dt;
			}
			updateVal2 = Mathf.Clamp01(updateVal2);

			SetBlendTargetWeight("Belly", updateVal);
			SetBlendTargetWeight("Boobs", updateVal2);
		}

		public void SetBlendLerp(string name, bool enabled, float speed = 1.0f)
		{
			if (bodyBlendControls.ContainsKey(name))
			{
				foreach (var item in bodyBlendControls[name])
				{
					item.SetLerp(enabled, speed);
				}
			}
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
	}

	internal class BodyBlendControl : UnityEngine.Object
	{
		private static readonly FieldInfo dynBoneParticlesField = typeof(DynamicBone).GetField("m_Particles", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo dynBoneTotalLengthField = typeof(DynamicBone).GetField("m_BoneTotalLength", BindingFlags.NonPublic | BindingFlags.Instance);
		private struct DynBoneValue
		{
			public float m_Damping;
			public float m_Elasticity;
			public float m_Stiffness;
			public float m_Inert;

			public DynBoneValue(float d, float e, float s, float i)
			{
				m_Damping = d;
				m_Elasticity = e;
				m_Stiffness = s;
				m_Inert = i;
			}
		}

		public Dictionary<int, AnimationCurve> blendShapeControls = new Dictionary<int, AnimationCurve>();
		// Mode for calculating target weight
		public WeightMode targetWeightMode = WeightMode.MAXIMUM;

		// This is for controlling how inert the bone is as weight gets updated
		private List<DynamicBone> associatedDynBones = null;
		private AnimationCurve dynBoneInertCurve = null;
		private AnimationCurve dynBoneElasticityCurve = null;
		private AnimationCurve dynBoneStiffnessCurve = null;
		private AnimationCurve dynBoneDampingCurve = null;
		// Determine how bone parameters are adjusted from weight
		public DynBoneControlMode dynBoneControlMode = DynBoneControlMode.BASE_TO_ONE;

		private SkinnedMeshRenderer targetRenderer;

		private bool useLerp = true;
		private float lerpSpeed = 1.0f;
		private float boneUpdateInterval = 0.015f;

		private List<DynBoneValue> defaultDynBoneValues = null;
		private float elapsedTime = 0f;
		private float currentWeight = 0f;
		private Dictionary<string, float> targetWeights = new Dictionary<string, float>() { { "Default", 0f } };

		public BodyBlendControl(SkinnedMeshRenderer targetRenderer = null)
		{
			this.targetRenderer = targetRenderer;
		}

		public void SetTargetRenderer(SkinnedMeshRenderer rederer)
		{
			targetRenderer = rederer;
		}

		public void SetLerp(bool enabled, float speed = 1.0f)
		{
			useLerp = enabled;
			lerpSpeed = speed;
		}

		public void SetAssociatedDynBones(
			List<DynamicBone> bones,
			AnimationCurve inertCurve = null,
			AnimationCurve elasticityCurve = null,
			AnimationCurve stiffnessCurve = null,
			AnimationCurve dampingCurve = null)
		{
			if (bones == null || bones.Count <= 0)
				return;
			associatedDynBones = bones;
			dynBoneInertCurve = inertCurve;
			dynBoneElasticityCurve = elasticityCurve;
			dynBoneStiffnessCurve = stiffnessCurve;
			dynBoneDampingCurve = dampingCurve;
			SaveDynBoneValues(associatedDynBones);
		}

		public void SetBoneUpdateInterval(float interval)
		{
			boneUpdateInterval = interval;
		}

		public void SetTargetWeight(float weight, string source)
		{
			targetWeights[source] = weight;
		}

		public void RemoveTargetWeight(string source)
		{
			targetWeights.Remove(source);
		}

		public void Update(float t, bool forceNoLerp = false)
		{
			// Check if dynamic bones need updating
			elapsedTime += t;
			bool doBoneUpdate = false || forceNoLerp;
			if (boneUpdateInterval <= 0f)
			{
				doBoneUpdate = true;
			}
			else if (elapsedTime >= boneUpdateInterval)
			{
				elapsedTime -= boneUpdateInterval;
				elapsedTime = Mathf.Min(elapsedTime, boneUpdateInterval);
				doBoneUpdate = true;
			}

			if (useLerp && !forceNoLerp)
				currentWeight = Mathf.Lerp(currentWeight, GetTargetWeight(), t * lerpSpeed);
			else
				currentWeight = GetTargetWeight();

			// Update BlendShapes
			foreach (var item in blendShapeControls)
			{
				float value = item.Value.Evaluate(currentWeight);
				targetRenderer.SetBlendShapeWeight(item.Key, value * 100f);
			}

			if (doBoneUpdate && associatedDynBones != null)
			{
				SetAssociatedBoneValues();
			}
		}

		private void SetAssociatedBoneValues()
		{
			float inertValue = dynBoneInertCurve != null ?
				Mathf.Clamp01(dynBoneInertCurve.Evaluate(currentWeight)) : 0f;
			float elasticityValue = dynBoneElasticityCurve != null ?
				Mathf.Clamp01(dynBoneElasticityCurve.Evaluate(currentWeight)) : 0f;
			float stiffnessValue = dynBoneStiffnessCurve != null ?
				Mathf.Clamp01(dynBoneStiffnessCurve.Evaluate(currentWeight)) : 0f;
			float dampingValue = dynBoneDampingCurve != null ?
				Mathf.Clamp01(dynBoneDampingCurve.Evaluate(currentWeight)) : 0f;

			for (int i = 0; i < associatedDynBones.Count; i++)
			{
				switch (dynBoneControlMode)
				{
					case DynBoneControlMode.BASE_TO_ONE:
						associatedDynBones[i].m_Inert = Mathf.Lerp(defaultDynBoneValues[i].m_Inert, 1f, inertValue);
						associatedDynBones[i].m_Elasticity = Mathf.Lerp(defaultDynBoneValues[i].m_Elasticity, 1f, elasticityValue);
						associatedDynBones[i].m_Stiffness = Mathf.Lerp(defaultDynBoneValues[i].m_Stiffness, 1f, stiffnessValue);
						associatedDynBones[i].m_Damping = Mathf.Lerp(defaultDynBoneValues[i].m_Damping, 1f, dampingValue);
						break;
					case DynBoneControlMode.ZERO_TO_BASE:
						associatedDynBones[i].m_Inert = Mathf.Lerp(0f, defaultDynBoneValues[i].m_Inert, inertValue);
						associatedDynBones[i].m_Elasticity = Mathf.Lerp(0f, defaultDynBoneValues[i].m_Elasticity, elasticityValue);
						associatedDynBones[i].m_Stiffness = Mathf.Lerp(0f, defaultDynBoneValues[i].m_Stiffness, stiffnessValue);
						associatedDynBones[i].m_Damping = Mathf.Lerp(0f, defaultDynBoneValues[i].m_Damping, dampingValue);
						break;
					case DynBoneControlMode.FULL_CONTROL:
						associatedDynBones[i].m_Inert = Mathf.Lerp(0f, 1f, inertValue);
						associatedDynBones[i].m_Elasticity = Mathf.Lerp(0f, 1f, elasticityValue);
						associatedDynBones[i].m_Stiffness = Mathf.Lerp(0f, 1f, stiffnessValue);
						associatedDynBones[i].m_Damping = Mathf.Lerp(0f, 1f, dampingValue);
						break;
				}
				UpdateDynBoneParameters(associatedDynBones[i]);
			}
		}

		private float GetTargetWeight()
		{
			switch (targetWeightMode)
			{
				case WeightMode.MAXIMUM:
					return targetWeights.Count > 0 ? targetWeights.Values.Max() : 0f;
				case WeightMode.MINIMUM:
					return targetWeights.Count > 0 ? targetWeights.Values.Min() : 0f;
				case WeightMode.AVERAGE:
					return targetWeights.Count > 0 ? targetWeights.Values.Average() : 0f;
			}
			return 0f;
		}

		private void SaveDynBoneValues(List<DynamicBone> source)
		{
			if (defaultDynBoneValues == null)
				defaultDynBoneValues = new List<DynBoneValue>();

			foreach (DynamicBone item in source)
			{
				defaultDynBoneValues.Add(new DynBoneValue(
					item.m_Damping,
					item.m_Elasticity,
					item.m_Stiffness,
					item.m_Inert
				));
			}
		}

		private static void UpdateDynBoneParameters(DynamicBone bone)
		{
			var particles = bone.m_Particles;

			for (int i = 0; i < particles.Count; ++i)
			{
				DynamicBone.Particle p = particles[i];
				p.m_Damping = bone.m_Damping;
				p.m_Elasticity = bone.m_Elasticity;
				p.m_Stiffness = bone.m_Stiffness;
				p.m_Inert = bone.m_Inert;
				p.m_Radius = bone.m_Radius;

				var length = bone.m_BoneTotalLength;

				if (length > 0)
				{
					float a = p.m_BoneLength / length;
					if (bone.m_DampingDistrib != null && bone.m_DampingDistrib.keys.Length > 0)
						p.m_Damping *= bone.m_DampingDistrib.Evaluate(a);
					if (bone.m_ElasticityDistrib != null && bone.m_ElasticityDistrib.keys.Length > 0)
						p.m_Elasticity *= bone.m_ElasticityDistrib.Evaluate(a);
					if (bone.m_StiffnessDistrib != null && bone.m_StiffnessDistrib.keys.Length > 0)
						p.m_Stiffness *= bone.m_StiffnessDistrib.Evaluate(a);
					if (bone.m_InertDistrib != null && bone.m_InertDistrib.keys.Length > 0)
						p.m_Inert *= bone.m_InertDistrib.Evaluate(a);
					if (bone.m_RadiusDistrib != null && bone.m_RadiusDistrib.keys.Length > 0)
						p.m_Radius *= bone.m_RadiusDistrib.Evaluate(a);
				}

				p.m_Damping = Mathf.Clamp01(p.m_Damping);
				p.m_Elasticity = Mathf.Clamp01(p.m_Elasticity);
				p.m_Stiffness = Mathf.Clamp01(p.m_Stiffness);
				p.m_Inert = Mathf.Clamp01(p.m_Inert);
				p.m_Radius = Mathf.Max(p.m_Radius, 0);
			}
		}
	}
}

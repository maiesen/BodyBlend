# BodyBlend.dll Implementation Guide

This guide assumes that you have at least gone through the skin mod creation tutorial: [Creating skin for vanilla characters](https://github.com/risk-of-thunder/R2Wiki/wiki/Creating-skin-for-vanilla-characters-with-custom-model) and has built your mod in Unity.

If it was not clear, this is for Risk of Rain 2.

## Preparing your model for Unity BlendShapes
If you want to use BlendShapes, your model needs to have them first. The equivalent of BlendShapes in Blender is called Shape Keys. It is important that you check the option `Import BlendShapes` in the model import settings.

![Model Import Settings](https://cdn.discordapp.com/attachments/899633904833662987/899636326872256532/unknown.png)

You can test your BlendShapes by dragging your model file into the scene Hierarchy, clicking on the mesh with Skinned Mesh Renderer component, and changing the BlendShapes values.

![BlendShapes Weights](https://cdn.discordapp.com/attachments/899633904833662987/899637045750820864/unknown.png)

## Adding BodyBlend.dll as dependency.
In your Unity project, drag and drop the BodyBlend.dll file anywhere in your project. For organization purpose, you can create a `/Assets/Plugins` folder for storing the .dll files.

Next, locate the `/Assets/SkinMods/[YourModName]` folder. Inside it, you should be able to find a `[YourModName].asmdef` file. Click on it to find **Assembly References** in the inspector.

![Assembly References](https://cdn.discordapp.com/attachments/899633904833662987/899634119863046194/unknown.png)

Press the `+` button and select BodyBlend.dll you have added earlier. Scroll down to the buttom and hit apply.

Now we are ready to set up the blending behavior in code.

## Setting up soft dependency for BodyBlend.dll
It is reasonable to assume that you still want the mod to work even when BodyBlend.dll is not loaded. For this, we will be setting up a soft dependency for BodyBlend.dll. For more information, you can follow this [Link](https://github.com/risk-of-thunder/R2Wiki/wiki/Mod-Compatibility:-Soft-Dependency).

Before continuing, make sure to have your `Edit>Preferences>External Tools` set to use Visual Studio as an external script editor.

![External Tools Setting](https://cdn.discordapp.com/attachments/899633904833662987/899639632487153735/unknown.png)

To begin, right click on `/Assets/SkinMods/[YourModName]` folder and select `Open C# Project` in the menu. Once Visual Studio has loaded with your mod project, right click on `/Assets/SkinMods/[YourModName]` folder in the Solution Explorer and select `Add>New Item`. Then, choose `Class` option from the list, set the name below to something like `BodyBlendCompatibility.cs`, and click on Add. Inside the newly added class, copy and paste the code below under `namespace YourModName` to replace the auto-generated class.

```C#
class BodyBlendCompatibility
{
    private static bool? _enabled;

    public static bool enabled
    {
        get
        {
            if (_enabled == null)
            {
                _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(BodyBlend.BodyBlendPlugin.GUID);
            }
            return (bool)_enabled;
        }
    }

    public static void RegisterBodyBlendSkin()
    {

    }
}
```

Also, add these assembly references to the top of the code:

```C#
using UnityEngine;
using static BodyBlend.Utils.BodyBlendUtils;
```

Now before we continue, we will need to take a slight detour to your generated plugin file `/Assets/SkinMods/[YourModName]/[YourModName]Plugin.cs`. Click on it to open it in Visual Studio.

Above the line with `[BepInPlugin(...)]`, copy and paste in the code below:
```C#
[BepInDependency(BodyBlend.BodyBlendPlugin.GUID, BepInDependency.DependencyFlags.SoftDependency)]
```
The resulting code should look something like this:
```C#
namespace YourModName
{
	[BepInDependency(BodyBlend.BodyBlendPlugin.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin("YourModNameGUID","YourModName","X.X.X")]
    public partial class YourModNamePlugin : BaseUnityPlugin
```

Lastly, copy the code below to the bottom of the `Awake()` method, after the line with `AfterAwake();`.

```C#
if (BodyBlendCompatibility.enabled)
    BodyBlendCompatibility.RegisterBodyBlendSkin();
```

**WARNING**: The file `[YourModName]Plugin.cs` will be regenerated if you checked on the option `Regenerate Code` in your SkinModInfo and hit build. You may need to redo the steps above whenever you add/remove skin from your mod and had to regenerate the code. Additionally, the `.asmdef` file we have modified earlier will be regenerated if you has the `Regenerate Assembly Dll` checked. Uncheck it to avoid editing the `.asmdef` again.

![SkinModInfo Regen Code](https://cdn.discordapp.com/attachments/899633904833662987/899647966451728424/unknown.png)

## Registering the controls for your BlendShapes
With the soft dependency taken care of, we can head back to `BodyBlendCompatibility.cs` to register your BlendShapes controls. All you need to do is modify the code in `RegisterBodyBlendSkin()` following the format below. The explanations will be in the code comments.

```C#
public static void RegisterBodyBlendSkin()
{
    // Create a new template.
    BlendControlTemplate template = new BlendControlTemplate();
    // Set the renderer index.**
    template.targetRendererIndex = 0;
    // Set controls for each of the blend shape.
    // The 0 in blendShapeControls[0] corresponds to the index of your BlendShapes.
    template.blendShapeControls[0] = MakeAnimationCurve(
        // Here you will set the keyframes of the animation curve.
        // This will determines how much influence the specified BlendShape has at different input values.
        // The input value (first number) should range from 0f to 1f.
        // The output value (second number) should range from 0f to 1f.
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f));
    // You can add multiple controls to the same "BlendGroup".
    template.blendShapeControls[1] = MakeAnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 1f, 0f, 0f),
        new Keyframe(1f, 0f));

    // Here you can set if you want to change dynamic bone parameters as the input values change. This part is optional.
    // First you need to define what dynamic bones will be affected.
    template.associatedDynBoneNames.AddRange(new string[] { "breast.l", "breast.r" });
    // Then define the animation curves like the blendShapeControls above
    template.dynBoneInertCurve = MakeAnimationCurve(
            new Keyframe(0f, 0.75f),
            new Keyframe(1f, 0f));
    template.dynBoneElasticityCurve = MakeAnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.4f, 0f),
            new Keyframe(1f, 0f));
    // template.dynBoneStiffnessCurve = MakeAnimationCurve()
    // template.dynBoneDampingCurve = MakeAnimationCurve()

    // These are also optional.
    // Set how fast the shape changes to the input value.
        // template.lerpSpeed = 2.0f;
    // Set if you want to use lerp (smooth transition) or not (instant transition).
        // template.useLerp = true;
    // Choose how to calculate the final input value from many inputs.
        // template.targetWeightMode = BodyBlend.WeightMode.MAXIMUM;

    // Register your control template(s)
    // Use skin.nameToken found in your [YourModName]Plugin.cs in the first param.
    // Set the name of your "BlendGroup" in the second param. This is what other mods will use to set the input value at the correct place.
    RegisterSkinBlendControl("YOUR_SKINDEF_NAME_TOKEN", "BlendGroupName", template);
    // If you want to add multiple templates (each affecting different meshes) to the same "BlendGroup", you can do so by using List<BlendControlTemplate>
    // RegisterSkinBlendControl("YOUR_SKINDEF_NAME_TOKEN", "BlendGroupName", templates);
}
```

\*\*In order to find which index to use, you can check the tables found [here](https://github.com/risk-of-thunder/R2Wiki/wiki/Creating-skin-for-vanilla-characters-with-custom-model#renderers) under `Renderers`. When counting the renderer indices, skip the ones that doesn't exist in display model. For example, `MageMesh` is index 3 because `Fire, JetsL, JetsR, FireRing` do not exist in display model (7 - 4 = 3).

**Congratulations, you are now done with setting up your model. The next part will be about how to set the input values (possibly from other mods though you can also put it in with the skin mod.)**

## Controlling the BlendShapes
After the difficult task of setting up the BlendShape controls, using it is much simpler. All you need is a reference to the character model object and a bit of code.

```C#
// Assuming you have reference to the CharacterBody component (body)
// You can get BodyBlendController component from the character model object.
BodyBlendController controller = body.modelLocator.modelTransform.gameObject.GetComponent<BodyBlendController>();

float value = GetValue();
// Set the target weight (input) with value.
// "Source" is for identifying where the input value come from. It is useful in case multiple mods try to set input to the same BlendGroup
if (controller)
    controller.SetBlendTargetWeight("BlendGroupName", value, "Source");
```
And that's it!

## Notes
Currently the BodyBlend mod is a WIP. Many things are subject to change. Though for my own testing purposes, I have set up the controller to respond to two blend group names: "Belly" and "Boobs". These can be controlled by pressing `<`/`>` and `k`/`l` respectively.


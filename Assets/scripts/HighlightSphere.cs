using UnityEngine;

// This attribute makes the script run even if the engine is NOT in play mode!
// BUT WATCH OUT! If you mistakenly put an infinite loop in the Update() function, it will crash the whole editor!
 [ExecuteAlways] // I've disabled it now so I don't accidentally change colors while testing in the editor before play mode!
public class HighlightSphere : MonoBehaviour
{
    /**
    * Sooo it turns out that Setting the HDR color glow intensity programmatically via code is not as easy as the HDR slider on the material color picker!
    * What I usually do when I want to replicate a Unity Editor feature that I cannot find in C# or in the documentation
    * I go into the Unity Editor code on GitHub and look for that feature, and see how they implemented it!
    * Here is the code for the HDR color picker, which is the ColorMutator class in the Unity Editor:
    *   https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/GUI/ColorMutator.cs
    **/
    [Range(1.0f, 10.0f)] public float glowIntensity = 1.0f; // Adding [Range(min, max)] before a numeric (public or serializable) variable adds a slider to the inspector so you can make live changes easily!
    public bool animate = true;
    [Range(0.05f, 1.0f)] public float animationSpeed = 0.5f;
    public Color baseColor;
    public Color mutatedColor;
    public ColorMutator colorMutator; // The cool thing is that the ColorMutator class has its own inspector in the Unity Editor, so you can see the changes in real-time!
    private Material glowMaterial = null;
    float t = 0.0f; // starting time value for the Lerp (Lerp => Linear intERPolation)
    float animationDirection = 1.0f; // 1.0f for increasing, -1.0f for decreasing time


    // Because this script is set to "ExecuteAlways", and Awake() gets called every time the script is loaded
    // We need to check if we already retreived the material from the object
    // GetComponent() is a very expensive operation in Unity, so we want to avoid it if we can!
    // Always cache your component after the first time you get it in Start() or Awake()!
    void Awake()
    {
        if(glowMaterial == null){
            glowMaterial = GetComponent<Renderer>().material;
            glowMaterial.EnableKeyword("_EMISSION"); // Apparently very important: https://discussions.unity.com/t/setting-emission-color-programatically/152813/2
        }
        if(glowMaterial != null){
            baseColor = glowMaterial.GetColor("_EmissionColor");
            mutatedColor = glowMaterial.GetColor("_EmissionColor");
        }
    }

    void Update()
    {
        if(animate){
            UpdateAnimatedIntensity();
        }
        UpdateMaterialColorIntensity(glowIntensity);
    }

    void UpdateAnimatedIntensity(float minimum = 1.0f, float maximum = 10.0f)
    {
        glowIntensity = Mathf.Lerp(minimum, maximum, t);

        t += animationSpeed * animationDirection * Time.deltaTime;

        // If our time value has passed 1.0f, we changed the animation direction to be negative so the value will start decreasing instead
        if (animationDirection > 0f && t > 1.0f)
        {
            glowIntensity = maximum;
            animationDirection = -1.0f;
        }
        if (animationDirection < 0f && t <= 0.0f)
        {
            glowIntensity = minimum;
            animationDirection = 1.0f;
        }

    }
    void UpdateMaterialColorIntensity(float glowIntensity){
        // Let's also make sure we have already retreived the material before trying to update it!
        if (glowMaterial != null)
        {
            // I've gone through the ColorMutator code and what I think we need to do is to create a new ColorMutator object with our object's color as its base color
            colorMutator = new(baseColor);

            /**
            * If you go to the ColorMutator code, you'll see that the exposureValue is the intensity of the color and whenever it's set, it updates the whole hdr color!
            * The exposureValue is basically the intensity of the color, i.e. the glow intensity
            * And it can be calculated as the RGB vector of the color multiplied by 2^intensity 
            * [ R,    G,    B ]  * 2^intensity
            * [1.0f, 0.0f, 0.0f] * 2^3 = [8.0f, 0.0f, 0.0f] 
            **/
            colorMutator.exposureValue = glowIntensity;

            // Now we need to set the color of the object's material with the newly mutated Color
            mutatedColor = colorMutator.exposureAdjustedColor;
            glowMaterial.SetColor("_EmissionColor", mutatedColor);
        }
    }
}

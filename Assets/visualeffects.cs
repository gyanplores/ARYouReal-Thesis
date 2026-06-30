using UnityEngine;

/*
=====================================

Attach to:
ROOT DRONE OBJECT

Features:
- Propeller rotation
- LED emission
- Idle pulse animation

After attaching:

1. Expand Propellers
2. Drag propeller transforms

3. Expand LED Renderers
4. Drag LED mesh renderers

=====================================
*/

public class VisualEffects : MonoBehaviour
{
    /*
    ==========================
    PROPELLERS
    ==========================
    */

    [Header("Propellers")]

    [Tooltip("Drag propeller transforms here")]
    [SerializeField]
    private Transform[] propellers;

    [Tooltip("Clockwise rotation")]
    [SerializeField]
    private bool[] clockwise;

    [Tooltip("Degrees per second")]
    [SerializeField]
    private float propellerSpeed = 3000f;

    //test
    [SerializeField]
    private Vector3[] rotationAxis;

    /*
    ==========================
    LEDS
    ==========================
    */

    [Header("LED Lights")]

    [Tooltip("Drag LED renderers here")]
    [SerializeField]
    private Renderer[] ledRenderers;

    /*[Tooltip("Emission colors")]
    [SerializeField]
    private Color[] ledColors;*/

    [Tooltip("Glow intensity")]
    [SerializeField]
    [Range(0,100)]
    private float ledIntensity = 25f;

    [Tooltip("Blink frequency")]
    [SerializeField]
    private float blinkSpeed = 4f;

    [Tooltip("Smooth pulse instead")]
    [SerializeField]
    private bool isPulsing = false;

    //test
    private Material[] ledMaterials;

    [SerializeField]
    private Material[] ledOnMaterials;

    [SerializeField]
    private Material[] ledOffMaterials;

    void Start()
    {
        ledMaterials = new Material[ledRenderers.Length];

        for (int i = 0; i < ledRenderers.Length; i++)
        {
            if (ledRenderers[i] == null)
                continue;

            ledMaterials[i] = ledRenderers[i].material;
            ledMaterials[i].EnableKeyword("_EMISSION");
        }
        //ConfigureLEDs();
    }



    void Update()
    {
        RotatePropellers();

        AnimateLEDs();
    }



    /*
    ==================================
    PROPELLERS
    ==================================
    */

    void RotatePropellers()
    {
        if (propellers == null)
            return;

        for (int i = 0; i < propellers.Length; i++)
        {
            if (propellers[i] == null)
                continue;

            float direction =
                (i < clockwise.Length && clockwise[i])
                ? 1f
                : -1f;

            Vector3 axis = (i < rotationAxis.Length)
            ? rotationAxis[i]
            : Vector3.forward;

            propellers[i].Rotate(
                axis,
                propellerSpeed *
                direction *
                Time.deltaTime,
                Space.Self
            );
        }
    }



    /*
    ==================================
    LEDS
    ==================================
    */

    void ConfigureLEDs()
    {
        foreach (Renderer r in ledRenderers)
        {
            if (r == null)
                continue;

            r.material.EnableKeyword(
                "_EMISSION"
            );
        }
    }

    void PulseMode(int index)
    {
        if (index < 0 || index >= ledRenderers.Length)
            return;

        if (ledRenderers[index] == null)
            return;

        bool isOn =
            isPulsing
            ? Mathf.Sin(Time.time * blinkSpeed) > 0
            : Mathf.Floor(Time.time * blinkSpeed) % 2 == 0;

        if (isOn)
        {
            if (index < ledOnMaterials.Length &&
                ledOnMaterials[index] != null)
            {
                ledRenderers[index].material =
                    ledOnMaterials[index];
            }
        }
        else
        {
            if (index < ledOffMaterials.Length &&
                ledOffMaterials[index] != null)
            {
                ledRenderers[index].material =
                    ledOffMaterials[index];
            }
        }
    }

    void AnimateLEDs()
    {
        PulseMode(0);//red led constant blinking
        ledRenderers[1].material = ledOnMaterials[1];
        ledRenderers[2].material = ledOnMaterials[2];
        PulseMode(3);//yellow led sometimes
        

        /*if (
            ledRenderers == null ||
            ledColors == null
        )
            return;*/

        /*float brightness;

        if (isPulsing)
        {
            brightness =
                Mathf.Lerp(
                    ledIntensity * 0.5f,
                    ledIntensity,
                    Mathf.Sin(
                        Time.time *
                        blinkSpeed
                    ) * 0.5f + 0.5f
                );
        }
        else
        {
            brightness =
                Mathf.Floor(
                    Time.time *
                    blinkSpeed
                ) % 2 == 0

                ?

                ledIntensity

                :

                0f;
        }*/


        //ledRenderers is the old one
        /*for (
            int i = 0;
            i < ledMaterials.Length;
            i++
        )
        {
            if (
                ledMaterials[i] == null
            )
                continue;



            Color color =
                (
                    i < ledColors.Length
                )

                ?

                ledColors[i]

                :

                Color.white;

            ledMaterials[i].SetColor("_EmissionColor", color * brightness);*/

        /*ledRenderers[i]
            .material
            .SetColor(
                "_EmissionColor",
                color *
                brightness
            );*/
        /*}*/
    }
}
using System.Collections.Generic;
using Unity.VisualScripting;
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
    class Propeller
    {
        public Transform propeller;
        public bool clockwise;
        public Vector3 rotationAxis;
    }

    private Propeller[] propellers;

    [Header("Propellers")]

    [Tooltip("Degrees per second")]
    [SerializeField]
    private float propellerSpeed = 3000f;

    /*
    ==========================
    LEDS
    ==========================
    */

    class LED
    {
        public Renderer renderer;
        public Light pointLight;

        public Material onMaterial;
        public Material offMaterial;
    }

    private LED[] leds;

    void Start()
    {
        //LED SETUP

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            Debug.Log(r.name);
        }

        List<LED> foundLEDs = new List<LED>();

        Material offMaterial =
            Resources.Load<Material>("Materials/LED_glow off");

        foreach (Renderer r in renderers)
        {
            if (!r.name.ToLower().StartsWith("led"))
                continue;

            LED led = new LED();

            led.renderer = r;

            // Finds the Point Light underneath this LED mesh
            led.pointLight = r.GetComponentInChildren<Light>(true);

            string n = r.name.ToLower();

            if (n.Contains("red"))
                led.onMaterial =
                    Resources.Load<Material>("Materials/LED_glow red");

            else if (n.Contains("yellow"))
                led.onMaterial =
                    Resources.Load<Material>("Materials/LED_glow yellow");

            else if (n.Contains("green"))
                led.onMaterial =
                    Resources.Load<Material>("Materials/LED_glow green");

            else if (n.Contains("blue"))
                led.onMaterial =
                    Resources.Load<Material>("Materials/LED_glow blue");

            led.offMaterial = offMaterial;

            foundLEDs.Add(led);
        }

        leds = foundLEDs.ToArray();

        //Constant light up
        SetLED(0, true);//led 5 green (pcb)
        SetLED(1, true);//led 6 blue (pcb)
        SetLED(2, true);//led 2 blue
        SetLED(3, true);//led 3 blue

        //PROPELLER SETUP

        List<Propeller> foundPropellers = new List<Propeller>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.ToLower().StartsWith("rotor"))
                continue;

            Propeller p = new Propeller();

            p.propeller = t;

            // Default axis
            p.rotationAxis = Vector3.up;

            foundPropellers.Add(p);
        }

        propellers = foundPropellers.ToArray();

        Debug.Log("Found " + propellers.Length + " propellers.");
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
        foreach (Propeller p in propellers)
        {
            float direction = p.clockwise ? 1f : -1f;

            p.propeller.Rotate(
                p.rotationAxis,
                direction * propellerSpeed * Time.deltaTime,
                Space.Self
            );
        }
    }

    /*
    ==================================
    LEDS
    ==================================
    */
    void SetLED(int index, bool on)
    {
        if (index < 0 || index >= leds.Length)
            return;

        LED led = leds[index];

        if (on)
            led.renderer.material = led.onMaterial;
        else
            led.renderer.material = led.offMaterial;

        if (led.pointLight != null)
            led.pointLight.enabled = on;
    }

    void PulseMode(int index, float onTime, float offTime)
    {
        float cycle = onTime + offTime;

        bool isOn = (Time.time % cycle) < onTime;

        SetLED(index, isOn);
    }

    void AnimateLEDs()
    {
        PulseMode(5, 4.0f, 0.1f);//led 4 yellow blinking sometimes
        PulseMode(2, 0.25f, 0.5f);//led 1 red blinking constant
    }
}
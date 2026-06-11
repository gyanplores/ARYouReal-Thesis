using UnityEngine;

public class PropellerSpin : MonoBehaviour
{
    public float speed = 3000f;

    void Update()
    {
        transform.Rotate(
            Vector3.up * speed * Time.deltaTime
        );
    }
}
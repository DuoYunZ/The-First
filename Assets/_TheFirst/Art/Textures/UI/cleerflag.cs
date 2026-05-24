using UnityEngine;

public class TransparentBackground : MonoBehaviour
{
    void OnPreRender()
    {
        GL.Clear(true, true, Color.clear);
    }
}
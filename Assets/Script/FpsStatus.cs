using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FpsStatus : MonoBehaviour
{
    [Range(10, 150)]
    public int fontSize = 30;
    public Color color = new Color(.0f, .0f, .0f, 1.0f);
    public float width, height;

    float updatetime = 0.3f;

    float time = 0.0f;

    Rect position;

    float fps;
    float ms;
    string text;

    GUIStyle style = new GUIStyle();

    void OnGUI()
    {
        time += Time.deltaTime;

        if (time > updatetime)
        {
            position = new Rect(width, height, Screen.width, Screen.height);

            fps = 1.0f / Time.deltaTime;
            ms = Time.deltaTime * 1000.0f;
            text = string.Format("{0:N1} FPS ({1:N1}ms)", fps, ms);

            style.fontSize = fontSize;
            style.normal.textColor = color;
            time -= updatetime;
        }

        GUI.Label(position, text, style);
    }
}
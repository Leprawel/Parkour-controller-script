using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HudTextScript : MonoBehaviour
{
    Text textBox;
    void Start()
    {
        textBox = GetComponent<Text>();
    }

    void Update()
    {

    }

    public void SetValue(float newVal)
    {
        textBox.text = newVal.ToString("F2");
    }
}

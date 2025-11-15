using UnityEngine;
using TMPro;

public class KeyOverlayCanvas : MonoBehaviour
{
    [Header("Key Text Objects")]
    public TextMeshProUGUI keyW;
    public TextMeshProUGUI keyA;
    public TextMeshProUGUI keyS;
    public TextMeshProUGUI keyD;
    public TextMeshProUGUI keyCtrl;

    [Header("Colors")]
    public Color normalColor = Color.gray;
    public Color pressedColor = Color.green;

    void Update()
    {
        keyW.color = Input.GetKey(KeyCode.W) ? pressedColor : normalColor;
        keyA.color = Input.GetKey(KeyCode.A) ? pressedColor : normalColor;
        keyS.color = Input.GetKey(KeyCode.S) ? pressedColor : normalColor;
        keyD.color = Input.GetKey(KeyCode.D) ? pressedColor : normalColor;
        keyCtrl.color = Input.GetKey(KeyCode.LeftControl) ? pressedColor : normalColor;
    }
}



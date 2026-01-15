using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SavedSettingsManager : MonoBehaviour
{
    public PaintManagerCustom paintManagerCustom;
    public Toggle[] resolutionToggles;

    // Start is called before the first frame update
    void Start()
    {
        int canvasNum = PlayerPrefs.GetInt("resolution");
        /*foreach(Toggle tog in resolutionToggles)
        {
            tog.enabled = false;
        }*/

        switch (canvasNum)
        {
            case 0:
                //paintManagerCustom.canvasSize = PaintManagerCustom.CanvasSize.size_1920x1080;
                resolutionToggles[0].isOn = true;
                //resolutionToggles[0].enabled = true;
                //Debug.Log("PlayerPrefs canvas sized to 1920x1080");
                break;
            case 1:
                //paintManagerCustom.canvasSize = PaintManagerCustom.CanvasSize.size_2560x1440;
                resolutionToggles[1].isOn = true;
                //resolutionToggles[1].enabled = true;
                //Debug.Log("PlayerPrefs canvas sized to 2560x1440");
                break;
            case 2:
                //paintManagerCustom.canvasSize = PaintManagerCustom.CanvasSize.size_5120x2880;
                resolutionToggles[2].isOn = true;
                //resolutionToggles[2].enabled = true;
                //Debug.Log("PlayerPrefs canvas sized to 5120x2880");
                break;
        }
    }
}

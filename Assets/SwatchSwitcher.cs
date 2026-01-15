using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwatchSwitcher : MonoBehaviour
{
    public GameObject[] colorCategories;

    public void SwitchColorCategory(int categoryNum)
    {
        foreach(GameObject category in colorCategories)
        {
            category.SetActive(false);

            colorCategories[categoryNum].SetActive(true);
        }
    }
}

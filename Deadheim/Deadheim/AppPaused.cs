using UnityEngine;

namespace Deadheim {
    public class AppPaused : MonoBehaviour
    {
        void OnGUI()
        {
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;
            Debug.Log("Focus");            
            Util.SavePlayer();
        }
    }
}


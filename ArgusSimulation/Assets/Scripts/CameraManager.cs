using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Caméras")]
    public Camera[] cameras;
    
    [Header("Paramètres")]
    public KeyCode switchKey = KeyCode.C;
    
    private int currentCameraIndex = 0;
    
    void Start()
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].gameObject.SetActive(i == 0);
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            SwitchCamera();
        }
    }
    
    void SwitchCamera()
    {
        // Désactiver la caméra actuelle
        cameras[currentCameraIndex].gameObject.SetActive(false);
        
        // Passer à la caméra suivante
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
        
        // Activer la nouvelle caméra
        cameras[currentCameraIndex].gameObject.SetActive(true);
        
        Debug.Log($"Caméra active : {cameras[currentCameraIndex].name}");
    }
}
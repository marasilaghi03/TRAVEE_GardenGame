using UnityEngine;

public enum ExergameUseType
{
    ClinicalUse = 0,
    HomeUse = 1
}

public class InputDataContainer : MonoBehaviour
{
    [SerializeField, TextArea] private string InputData;
    [SerializeField, TextArea] private string Parameters;
    [SerializeField] private Language Language;
    [SerializeField] protected ExergameUseType UseType;

    private void Start()
    {
#if UNITY_EDITOR

        if (UseType == ExergameUseType.HomeUse && GetComponent<InputContainerHomeUseSimulator>() == null)
            gameObject.AddComponent<InputContainerHomeUseSimulator>();
#endif


        var sessionGO = GameObject.Find("SessionContainer");
        if (sessionGO == null)
        {
            Debug.LogError("InputDataContainer: SessionContainer not found (must be named exactly 'SessionContainer').");
            return;
        }

        var json = Parameters;
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("InputDataContainer: Parameters JSON is empty.");
            return;
        }

        if (UseType == ExergameUseType.HomeUse)
            sessionGO.SendMessage("OnInitHomeUse", json, SendMessageOptions.RequireReceiver);
        else
            sessionGO.SendMessage("OnInit", json, SendMessageOptions.RequireReceiver);

        Debug.Log($"InputDataContainer: Sent init ({UseType}).");
    }
}

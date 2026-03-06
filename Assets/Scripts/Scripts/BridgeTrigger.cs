using UnityEngine;

public class BridgeTrigger : MonoBehaviour
{
    // Drag Truth_Table_rules from the hierarchy into here
    [SerializeField] private GameObject truthTableRulesGroup; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {   
            Debug.Log("Player Detected");
            if (truthTableRulesGroup != null) truthTableRulesGroup.SetActive(true);
            Destroy(gameObject);
        }
    }
}
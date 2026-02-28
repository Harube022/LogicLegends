using UnityEngine;

public class LogicRow : MonoBehaviour
{
    public bool inputA;
    public bool inputB;

    [SerializeField] private Transform snapPoint;

    private bool isFilled = false;

    public void LockIn(TruthBlock block)
    {
        isFilled = true;

        block.transform.position = snapPoint.position;
        block.transform.rotation = snapPoint.rotation;

        block.GetComponent<Rigidbody>().isKinematic = true;
        block.GetComponent<Collider>().enabled = false;
    }

    public bool IsFilled()
    {
        return isFilled;
    }
}
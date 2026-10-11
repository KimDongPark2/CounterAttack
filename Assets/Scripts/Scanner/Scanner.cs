using UnityEngine;

public class Scanner : MonoBehaviour
{
    public GameObject Inven;
    public GameObject ScanPoint;

    private Animator anim;
    private Animator Invenanim;

    private Collider zone;

    private bool wasInside = false;

    private void Start()
    {
        Invenanim = Inven.GetComponent<Animator>();
        anim = GetComponent<Animator>();
        zone = ScanPoint.GetComponent<Collider>();
    }
    private void Update()
    {
        bool inside = zone.bounds.Contains(transform.position);
        anim.SetBool("IsOpen", inside);
        Invenanim.SetBool("IsCounter", inside);
    }
}
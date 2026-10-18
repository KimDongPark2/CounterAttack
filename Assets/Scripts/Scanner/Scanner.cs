using UnityEngine;

// 계산대 범위(ScanPoint)에 들어가면 스캐너를 꺼내고, 인벤 UI를 계산대 모드로 바꿈
// 계산대 안에 있는지는 Player_State.inCounter에 적어둠 (권총 칸 숨기기에 씀)
public class Scanner : MonoBehaviour
{
    public GameObject Inven;
    public GameObject ScanPoint;

    private Animator anim;
    private Animator Invenanim;
    private Collider zone;
    private Player_State state;

    private void Start()
    {
        Invenanim = Inven.GetComponent<Animator>();
        anim = GetComponent<Animator>();
        zone = ScanPoint.GetComponent<Collider>();
        state = GetComponentInParent<Player_State>();
    }

    private void Update()
    {
        // 애니메이션에 흔들리지 않게 플레이어 본체 위치로 검사
        bool inside = zone.bounds.Contains(transform.root.position);
        if (state != null) state.inCounter = inside;
        bool gunInHand = state != null && state.hasGun && state.handIndex == Player_Interaction.GunIndex;

        anim.SetBool("IsOpen", inside && !gunInHand);
        Invenanim.SetBool("IsCounter", inside);
    }
}
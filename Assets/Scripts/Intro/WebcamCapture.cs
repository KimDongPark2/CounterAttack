using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 명찰 사진 칸
// 📷 버튼 → 카메라 켜짐 → 3, 2, 1 → 찰칵 (번쩍) → 사진 고정 + 카메라 꺼짐
// 다시 누르면 다시 찍기. 카메라가 없으면 기본 사진 그대로 (게임은 그냥 진행)
// 카메라는 📷을 누를 때만 켜짐 (명찰을 열자마자 켜지지 않음)
// Photo 오브젝트에 붙이기
public class WebcamCapture : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("사진 · 카메라 화면이 나오는 RawImage")]
    public RawImage view;
    [Tooltip("📷 버튼 (Camera Launch)")]
    public Button captureButton;
    [Tooltip("3, 2, 1 숫자 (선택)")]
    public TMP_Text countdownText;
    [Tooltip("찰칵할 때 번쩍이는 흰 판 (선택 · 사진 칸 크기의 흰 Image)")]
    public Image flash;
    [Tooltip("안 찍었을 때 보일 기본 사진 (비우면 View에 처음 들어 있던 그림)")]
    public Texture defaultTexture;

    [Header("사진")]
    [Tooltip("저장되는 사진 크기 (사진 칸 비율과 같게)")]
    public int photoWidth = 236;
    public int photoHeight = 280;
    public int countdown = 3;
    [Tooltip("셀카처럼 좌우 뒤집기")]
    public bool mirror = true;
    [Tooltip("쓸 카메라 이름의 일부 (비우면 자동)")]
    public string preferCamera = "";

    [Header("소리 (선택)")]
    public Sfx tickSound = Sfx.PrepTick;
    public Sfx shutterSound = Sfx.None;

    [Header("안내 (선택)")]
    public TMP_Text messageText;
    public string noCameraMessage = "카메라가 없어서 기본 사진으로 해요";
    public float messageTime = 2f;

    // 찍은 사진 (안 찍었으면 null)
    public Texture2D Photo { get; private set; }
    // 카운트다운 중인지
    public bool Busy { get; private set; }

    private WebCamTexture cam;
    private Coroutine routine;
    private float messageTimer;

    private void Awake()
    {
        if (view != null && defaultTexture == null) defaultTexture = view.texture;
        if (captureButton != null) captureButton.onClick.AddListener(Capture);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (flash != null)
        {
            flash.raycastTarget = false;
            flash.gameObject.SetActive(false);
        }
        if (messageText != null) messageText.text = "";
    }

    private void Update()
    {
        // 카메라 해상도는 켜고 나서 조금 뒤에 정해져서, 켜져 있는 동안 계속 맞춰줌
        if (cam != null && cam.isPlaying && view != null && view.texture == cam) view.uvRect = CamRect();

        if (messageText != null && messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;
            if (messageTimer <= 0f) messageText.text = "";
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        routine = null;
        Busy = false;
        StopCamera();
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (flash != null) flash.gameObject.SetActive(false);
    }

    // ---------- 📷 버튼 ----------
    public void Capture()
    {
        if (Busy || !isActiveAndEnabled) return;
        routine = StartCoroutine(CaptureRoutine());
    }

    // 명찰을 새로 열 때: 사진 지우고 기본 사진으로
    public void ResetPhoto()
    {
        StopAllCoroutines();
        routine = null;
        Busy = false;
        StopCamera();
        if (Photo != null) Destroy(Photo);
        Photo = null;
        ShowDefault();
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (flash != null) flash.gameObject.SetActive(false);
        if (messageText != null) messageText.text = "";
    }

    // 근무 시작할 때 사진을 넘겨받음 (넘긴 뒤엔 이 스크립트가 지우지 않음)
    public Texture2D TakePhoto()
    {
        Texture2D p = Photo;
        Photo = null;
        return p;
    }

    private IEnumerator CaptureRoutine()
    {
        Busy = true;

        if (!StartCamera())
        {
            Fail();
            yield break;
        }

        // 첫 화면이 들어올 때까지 (처음 몇 프레임은 까맣게 나옴)
        float wait = 0f;
        while ((cam.width <= 16 || !cam.didUpdateThisFrame) && wait < 4f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (cam.width <= 16)
        {
            Fail();
            yield break;
        }

        view.texture = cam;
        view.uvRect = CamRect();

        // 3, 2, 1
        if (countdownText != null) countdownText.gameObject.SetActive(true);
        for (int i = countdown; i >= 1; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = i.ToString();
                StartCoroutine(Punch(countdownText.transform));
            }
            SoundManager.Play(tickSound);
            yield return new WaitForSecondsRealtime(1f);
        }
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        // 찰칵
        Snap();
        StopCamera();
        view.texture = Photo;
        view.uvRect = new Rect(0f, 0f, 1f, 1f);
        SoundManager.Play(shutterSound);
        if (flash != null) yield return Flash();

        Busy = false;
        routine = null;
    }

    private void Fail()
    {
        StopCamera();
        ShowDefault();
        if (messageText != null)
        {
            messageText.text = noCameraMessage;
            messageTimer = messageTime;
        }
        Debug.Log("[WebcamCapture] 카메라를 못 켰어요. 카메라가 연결돼 있는지, 윈도우 설정 → 개인 정보 → 카메라에서 앱 접근이 켜져 있는지 확인하세요.");
        Busy = false;
        routine = null;
    }

    // ---------- 카메라 ----------
    private bool StartCamera()
    {
        if (cam != null && cam.isPlaying) return true;

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0) return false;

        string deviceName = devices[0].name;
        foreach (WebCamDevice d in devices)
        {
            if (!string.IsNullOrEmpty(preferCamera) && d.name.Contains(preferCamera)) { deviceName = d.name; break; }
            if (string.IsNullOrEmpty(preferCamera) && d.isFrontFacing) { deviceName = d.name; break; }
        }

        try
        {
            cam = new WebCamTexture(deviceName, 1280, 720, 30);
            cam.Play();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WebcamCapture] " + e.Message);
            StopCamera();
            return false;
        }
        return true;
    }

    private void StopCamera()
    {
        if (cam == null) return;
        if (cam.isPlaying) cam.Stop();
        if (view != null && view.texture == cam) ShowDefault();
        Destroy(cam);
        cam = null;
    }

    // 카메라 화면에서 사진 칸 비율만큼 가운데를 잘라서 보여줄 범위 (좌우 뒤집기 포함)
    private Rect CamRect()
    {
        float camAspect = cam.height > 0 ? (float)cam.width / cam.height : 1f;
        float target = (float)photoWidth / photoHeight;
        Rect r;
        if (camAspect > target)
        {
            float w = target / camAspect;
            r = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            float h = camAspect / target;
            r = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        if (mirror) r = new Rect(r.x + r.width, r.y, -r.width, r.height);
        if (cam.videoVerticallyMirrored) r = new Rect(r.x, r.y + r.height, r.width, -r.height);
        return r;
    }

    // 지금 카메라 화면을 사진 크기로 잘라서 저장
    private void Snap()
    {
        Rect r = CamRect();
        RenderTexture rt = RenderTexture.GetTemporary(photoWidth, photoHeight, 0);
        Graphics.Blit(cam, rt, new Vector2(r.width, r.height), new Vector2(r.x, r.y));

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        if (Photo != null) Destroy(Photo);
        Photo = tex;
    }

    private void ShowDefault()
    {
        if (view == null) return;
        view.texture = defaultTexture;
        view.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    // ---------- 연출 ----------
    private IEnumerator Flash()
    {
        flash.gameObject.SetActive(true);
        Color c = flash.color;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - t / 0.3f;
            flash.color = c;
            yield return null;
        }
        c.a = 1f;
        flash.color = c;
        flash.gameObject.SetActive(false);
    }

    private IEnumerator Punch(Transform t)
    {
        float time = 0f;
        while (time < 0.25f && t != null)
        {
            time += Time.unscaledDeltaTime;
            float s = 1f + 0.35f * (1f - Mathf.Clamp01(time / 0.25f));
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }
}

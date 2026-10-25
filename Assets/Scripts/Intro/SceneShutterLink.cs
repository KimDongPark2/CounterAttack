using System.Collections;
using UnityEngine;

// 버튼(On Click)이나 이벤트(Customer Spawner의 On Day End 등)에서 셔터 전환으로 씬 바꾸기
// 셔터(SceneShutter)는 다른 씬에서 넘어온 거라 이벤트 칸에 직접 못 끌어다 놓으니까, 이걸 대신 연결
// 사용: 아무 오브젝트에 붙이고 → 이벤트 + 누르기 → 이 오브젝트 → SceneShutterLink.Load()
public class SceneShutterLink : MonoBehaviour
{
    [Tooltip("갈 씬 이름 (Build Settings에 들어가 있어야 함)")]
    public string sceneName = "Intro";
    [Tooltip("부르고 나서 셔터를 내리기까지 기다리는 시간 (초). 하루 끝나고 잠깐 보여주고 싶을 때")]
    public float delay = 0f;

    private bool waiting;

    public void Load()
    {
        Load(sceneName);
    }

    public void Load(string name)
    {
        if (waiting) return;
        if (delay > 0f) StartCoroutine(LoadLater(name));
        else SceneShutter.Load(name);
    }

    private IEnumerator LoadLater(string name)
    {
        waiting = true;
        yield return new WaitForSecondsRealtime(delay);   // 일시정지(Time.timeScale = 0) 중에도 흘러감
        waiting = false;
        SceneShutter.Load(name);
    }
}
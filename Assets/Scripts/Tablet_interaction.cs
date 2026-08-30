using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Tablet_interaction : MonoBehaviour
{
    [Header("Component")]
    public Player_State state;
    public Player_Look look;
    public Player_movement move;
    [Header("Adjustment")]
    public GameObject[] OptionScreen;
    private int selectedIndex = 0;
    public int OptionRestrict = 4;
    //public float mouseSensitivity = 15f;
    [Header("UI")]
    public TextMeshProUGUI CurMoney;
    public TextMeshProUGUI CurDebt;

    public Slider MouseSlider;

    public TextMeshProUGUI state_money;
    public TextMeshProUGUI state_debt;
    public TextMeshProUGUI state_star;
    public TextMeshProUGUI state_Inven_cnt;
    public TextMeshProUGUI state_Move;
    public TextMeshProUGUI state_days;
    public TextMeshProUGUI state_totalsales;
    public TextMeshProUGUI state_process;


    private void Start()
    {
        Init();
    }
    private void Update()
    {
        CurMoney.text = $"µ·:{state.money.ToString()}$";
        CurDebt.text = $"ºú:{state.debt.ToString()}$";
        look.moveSensitivity = MouseSlider.value;

        state_money.text = $"{state.money}$";
        state_debt.text = $"{state.debt}$";
        state_star.text = $"{state.star}/5";
        state_Inven_cnt.text = $"{state.inventoryRestrict}/10";
        state_Move.text = $"{move.moveSpeed}/30";
        state_days.text = $"{state.days}ÀÏ";
        state_totalsales.text = $"{state.totalSales}$";
        state_process.text = $"¸¸Á· {state.goodProcess}°³ | ºÒ¸¸Á· {state.badProcess}°³";
    }
    private void Init()
    {
        state.Init();

        for(int i=0;i<OptionRestrict; i++)
            OptionScreen[i].SetActive(false);
        OptionScreen[0].SetActive(true);
    }
    public void OpenOption(int index)
    {
        if (index < 0 || index >= OptionScreen.Length) return;
        if (index == selectedIndex) return;

        OptionScreen[selectedIndex].SetActive(false);
        OptionScreen[index].SetActive(true);
        selectedIndex = index;
    }

    public void OnLoans(int value)
    {
        state.money += value;
        state.debt += value;
    }
}

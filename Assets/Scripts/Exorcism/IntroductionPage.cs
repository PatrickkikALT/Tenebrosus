using UnityEngine;

[CreateAssetMenu(menuName = "Tenebrosus/Book Introduction Page", fileName = "IntroductionPage")]
public class IntroductionPage : BookPage
{
    [SerializeField] private string title;
    [SerializeField] [TextArea] private string subtitle;
    [SerializeField] [TextArea(8, 30)] private string leftText;
    [SerializeField] [TextArea(8, 30)] private string rightText;

    public override string Title => title;
    public override string Subtitle => subtitle;
    public string LeftText => leftText;
    public string RightText => rightText;
}

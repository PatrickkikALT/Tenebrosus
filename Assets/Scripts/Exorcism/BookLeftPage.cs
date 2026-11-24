using System;
using TMPro;
using UnityEngine;

[Serializable]
public class BookLeftPage
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text flavor;
    [SerializeField] private TMP_Text verses;

    public TMP_Text Title => title;
    public TMP_Text Flavor => flavor;
    public TMP_Text Verses => verses;
}

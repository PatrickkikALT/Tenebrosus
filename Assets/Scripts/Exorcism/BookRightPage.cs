using System;
using TMPro;
using UnityEngine;

[Serializable]
public class BookRightPage
{
    [SerializeField] private TMP_Text verses;
    [SerializeField] private TMP_Text pageNumber;

    public TMP_Text Verses => verses;
    public TMP_Text PageNumber => pageNumber;
}

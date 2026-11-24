using UnityEngine;

public abstract class BookPage : ScriptableObject
{
    public abstract string Title { get; }
    public abstract string Subtitle { get; }
}

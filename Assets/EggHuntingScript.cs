using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EggHuntingScript : MonoBehaviour
{
    public Texture2D[] SpriteTextures;
    public Renderer[] SpriteHolders;

    public void Awake()
    {
        foreach(Renderer r in SpriteHolders)
            r.enabled = false;
    }

    public void Start()
    {
        // Generate Race
    }
}

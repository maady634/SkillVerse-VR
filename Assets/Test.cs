using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{

    int i = 25;

    int health = 5000;


    public GameObject sphere;

    public void HiTHero()
    {
        health = health - 100;
    }


    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Game is starting");

        sphere.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

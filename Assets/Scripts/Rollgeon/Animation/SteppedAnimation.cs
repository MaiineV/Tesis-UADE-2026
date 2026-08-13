using UnityEngine;

public class SteppedAnimation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public Animator AnimCon;

    public int FPS = 15;

    private float _time;


    private void OnValidate()
    {
        if(AnimCon == null)
        {
            AnimCon = GetComponent<Animator>();
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _time += Time.deltaTime;
        var updateTime=1f / FPS;    
        AnimCon.speed = 0;

        // A timeScale alto un solo frame puede deber varios steps: drenar todos
        // (con "if", el acumulador crece sin límite y el stepping se desfasa)
        // y avanzar el animator ese total en este frame para no perder ritmo.
        int steps = 0;
        while (_time > updateTime)
        {
            _time -= updateTime;
            steps++;
        }
        if (steps > 0)
        {
            AnimCon.speed = steps * updateTime / Time.deltaTime;
        }
    }
}

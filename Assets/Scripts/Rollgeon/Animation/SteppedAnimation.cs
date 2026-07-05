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

        if (_time > updateTime)
        {
           _time -= updateTime;
            AnimCon.speed = updateTime / Time.deltaTime;
        }   
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Timer : MonoBehaviour
{
    float timer = 0f;
    float currentTime = 0f;
    bool done = true;

    public void StartTimer()
    {
        currentTime = timer;
        done = false;
    }

    void FixedUpdate()
    {
        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
        }
        else
        {
            done = true;
        }
    }

    public float TargetTime()
    {
        return timer;
    }

    public float CurrentTime()
    {
        return currentTime;
    }

    public float FractionLeft()
    {
        return currentTime / timer;
    }

    public float FractionDone()
    {
        return (timer - currentTime) / timer;
    }

    public bool Done()
    {
        return done;
    }

    public void SetTime(float newVal)
    {
        timer = newVal;
    }
}

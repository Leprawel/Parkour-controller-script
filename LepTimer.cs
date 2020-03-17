using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LepTimer
{
    public class Timer : MonoBehaviour
    {
        float timer = 0f;
        float currentTime = 0f;
        bool done = true;


        Timer(float x)
        {
            timer = x;
        }

        public void StartTimer()
        {
            currentTime = timer;
            done = false;
        }

        void FixedUpdate()
        {
            if(currentTime > 0)
            {
                currentTime -= Time.deltaTime;
            }
            else{
                done = true;
            }
        }

    }
}

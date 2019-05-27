using System.Collections.Generic;
using UnityEngine;

namespace Sources.Tools
{
    public class Logger : MonoBehaviour
    {
        public float Offset = 160;
        public int DrawCount = 15;
        public List<string> Messages = new List<string>();
        public static Logger I;

        private readonly object _locker = new object();

        private void Awake()
        {
            I = this;
        }

        public void Log(object caller, string message)
        {
            lock (_locker)
            {
                Messages.Add($"{caller.GetType().Name}: {message}");
            }
        }
        
        public void Log(string caller, string message)
        {
            lock (_locker)
            {
                Messages.Add($"{caller}: {message}");
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(Offset);
            GUILayout.BeginVertical(GUI.skin.box);
            var counter = 0;
            for (var i = Messages.Count - 1; i >= 0; i--)
            {
                counter++;
                var message = Messages[i];
                GUILayout.Label(message);
                if(counter==DrawCount)
                    break;
            }
            GUILayout.EndVertical();
        }
    }
}
using UnityEngine;

/// <summary>Persistent host so static UI motion helpers can run coroutines without a scene MonoBehaviour.</summary>
public class TrucoMotionRunner : MonoBehaviour
{
    static TrucoMotionRunner _instance;

    public static TrucoMotionRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[TrucoMotionRunner]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TrucoMotionRunner>();
            }
            return _instance;
        }
    }
}

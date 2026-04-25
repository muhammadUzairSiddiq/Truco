using UnityEngine;

public class SessionUser : MonoBehaviour
{
    [SerializeField] private User _currentUser;
   
    public User Data
    {
        get
        {

            if (_currentUser == null)
                Debug.Log("[SessionUser] - Current user is null. User might not be logged in.");

            return _currentUser;
        }
    }

    public void UpdateUserData(User user)
    {
        _currentUser = user;
    }

    public void Logout()
    {
        _currentUser = null;
    }

}

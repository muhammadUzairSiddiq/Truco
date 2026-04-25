using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;

    public void LoginAdmin()
    {
        string email = emailField.text;
        string password = passwordField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Email or Password cannot be empty.");
            return;
        }
        
        // if(email.Equals(DataHandler.Instance.IngameData.adminEmail) && 
        //    password.Equals(DataHandler.Instance.IngameData.adminPassword))
        // {
        //     DataHandler.Instance.IngameData.isAdmin = true;
        //     SceneManager.LoadScene("AdminScene");
        // }
        // else
        // {
        //     Debug.LogError("Invalid email or password.");
        // }
        
    }
    
}

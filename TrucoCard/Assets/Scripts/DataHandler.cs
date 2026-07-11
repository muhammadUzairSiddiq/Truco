using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class DataHandler : SingeltonBase<DataHandler>
{
	[SerializeField] private string Name; 
	//public UserDto IngameData {  set; get; }
	private BinaryFormatter formatter = new BinaryFormatter();
	private FileStream _stream;
	private string path;
	public int points { set; get; }
	public int opponentPoints { set; get; }
	public int roundNumber;
	bool Data_avaliable;
	
	public override void Awake()
	{
		base.Awake();
		path = Application.persistentDataPath + "/" + Name + ".data";
	}

	public void TryToLoadData(string _dataName)
	{
		Name = _dataName;
		path = Application.persistentDataPath + "/" + Name + ".data";
		LoadData();
	}

    public void LoadData()
	{
		try
		{
			if (File.Exists(path))
			{
				_stream = new FileStream(path, FileMode.Open);
				User userData = (User)formatter.Deserialize(_stream);
                ApiController.GetSessionUser.UpdateUserData(userData);

                _stream.Close();
                Data_avaliable = true;

            }
			else
			{
                ApiController.GetSessionUser.UpdateUserData(new User());

                Data_avaliable = false; 
            }
			
		}
		catch (Exception e)
		{
			Debug.Log(e.Message);
		}
	}

	public bool isFileExits()
	{
		return Data_avaliable;

    }
	
	public void SaveData()
	{ 
		try
		{
			_stream = new FileStream(path, FileMode.Create);
			formatter.Serialize(_stream,ApiController.GetSessionUser.Data);
			_stream.Close();
		}
		catch (Exception e)
		{
			_stream.Close();
			Console.WriteLine(e);
			//throw;
			Debug.Log(e);
		}
	}
	
	private void OnApplicationQuit()
	{
		//SaveData();
	}

}

[System.Serializable]
public class Data
{
	public bool loggedIn;
	public string userId;
	public UserStats userStats;
	public string playFabID;
	public Data()
	{
		loggedIn = false;
		userId = string.Empty;
	}
}

[System.Serializable]
public class UserStats
{
	public int totalGamesPlayed;
	public int totalGamesWon;
	public int totalGamesLost;
	public int totalGamesDrawn;
	public float coins;

	public UserStats()
	{
		totalGamesPlayed = 0;
		totalGamesWon = 0;
		totalGamesLost = 0;
		totalGamesDrawn = 0;
		coins = 1500; // Default starting coins
	}
	
	public UserStats(string userId)
	{
		totalGamesPlayed = 0;
		totalGamesWon = 0;
		totalGamesLost = 0;
		totalGamesDrawn = 0;
		coins = 1500;
	}

	public void ResetStats()
	{
		totalGamesPlayed = 0;
		totalGamesWon = 0;
		totalGamesLost = 0;
		totalGamesDrawn = 0;
		coins = 0;
	}
}



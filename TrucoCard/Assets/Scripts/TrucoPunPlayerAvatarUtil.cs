using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class TrucoPunPlayerAvatarUtil
{
    public const string AvatarKey = "avatarIndex";

    public static void ApplyLocalPlayerAvatar()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null) return;
        int idx = PlayerAvatarData.SelectedIndex;
        var h = new Hashtable { { AvatarKey, idx } };
        if (ApiController.GetSessionUser?.Data?._id != null)
            h["userId"] = ApiController.GetSessionUser.Data._id;
        PhotonNetwork.LocalPlayer.SetCustomProperties(h);
    }
}

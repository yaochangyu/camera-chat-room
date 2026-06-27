using System.Threading.Tasks;

namespace ChatRoom.Hubs
{
    public interface IChatClient
    {
        Task ReceiveMessage(string user, string message);
        Task ReceivePrivateMessage(string sender, string message); // 接收私訊 (步驟三)
        Task UpdateUserList(string[] users);                       // 更新在線使用者名單 (步驟三)
        Task UserJoined(string connectionId, string roomName);
        Task UserLeft(string connectionId, string roomName);
        Task ReceiveNotification(string message);
        Task FriendStatusChanged(string username, bool isOnline, string? statusMessage); // 好友在線狀態與狀態訊息變更通知 (步驟四)
        Task ReceiveCall(string callerUsername, string sdpOffer, string callType);
        Task CallAnswered(string callerUsername, string sdpAnswer);
        Task CallRejected(string callerUsername);
        Task CallEnded(string username);
        Task ReceiveICECandidate(string username, string candidate);
    }
}

using System.Runtime.Serialization;

namespace RemotePlugins
{

    internal class ClientOptions : RemoteObject
    {
        internal enum Synchronization
        {
            [EnumMember(Value = "UPDATE_ONLY")]
            UpdateOnly,

            [EnumMember(Value = "DELETE_AND_SYNC")]
            DeleteAndSync
        }

        public Synchronization SyncType { get; set; } = Synchronization.DeleteAndSync;
    }
}
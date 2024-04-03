using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
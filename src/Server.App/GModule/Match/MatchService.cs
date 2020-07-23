using DotNetty.Codecs.Mqtt.Packets;
using Fenix;
using Fenix.Common;
using Fenix.Common.Attributes;
using Shared;
using Shared.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using Server.UModule;
using Shared.DataModel;

namespace Server.GModule
{
    [RuntimeData(typeof(MatchData))]
    public partial class MatchService : Service
    {
        public MatchService(string name): base(name)
        {

        }

        public void onLoad()
        {

        }

        //public new string UniqueName => nameof(MatchService);

        [ServerApi] 
        public void JoinMatch(string uid, int match_type, Action<ErrCode> callback)
        {
            Log.Info("Call=>server_api:JoinMatch");
            callback(ErrCode.OK);
        } 

        [ServerOnly]
        [CallbackArgs("code", "user")]
        public void FindMatch(string uid, Action<ErrCode, Account> callback)
        {
            callback(ErrCode.OK, null);
        }
    }
}

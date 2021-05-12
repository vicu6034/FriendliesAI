using System.Collections.Generic;

namespace FriendliesAI
{
    public class MobConfig
    {
        public IEnumerable<ItemDrop> PreTameConsumables { get; set; }

        public IEnumerable<ItemDrop> PostTameConsumables { get; set; }

        public float PreTameFeedDuration { get; set; }

        public float PostTameFeedDuration { get; set; }

        public float TamingTime { get; set; }

        public string AIType { get; set; }

        public string AIConfig { get; set; }
    }
}

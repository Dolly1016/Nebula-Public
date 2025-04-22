using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

public class Versioning
{
    int currentVersion = 0;
    public void Update()=> currentVersion++;
    public Timestamp GetTimestamp(bool uninitialized = true) => new Timestamp(this, uninitialized ? -1 : currentVersion);
    public struct Timestamp
    {
        public int timestamp = -1;
        private Versioning versioning;

        public bool Check()
        {
            var result = IsUnchecked;
            timestamp = versioning.currentVersion;
            return result;
        }

        public bool IsUnchecked => versioning.currentVersion > timestamp;

        public Timestamp(Versioning version, int timestamp = -1){
            versioning = version;
            this.timestamp = timestamp;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RIP_KT3
{
    internal class Link
    {
        public int networkID { get; }
        public static int networkIDCounter = 1;
        public bool up;

        public Router r1;
        public int ix1;

        public Router r2;
        public int ix2;

        public Link(Router r1, Router r2, int interface1, int interface2, bool up = true)
        {
            networkID = networkIDCounter;
            ++networkIDCounter;

            this.r1 = r1;
            this.r2 = r2;

            this.ix1 = interface1;
            this.ix2 = interface2;

            this.up = up;
        }

        public Router GetOtherEndpoint(Router r)
        {
            return r == r1 ? r2 : r1;
        }

        public int GetOtherEndpointInterface(Router r)
        {
            return r == r1 ? ix2 : ix1;
        }

        public int GetEndpointInterface(Router r)
        {
            return r == r1 ? ix1 : ix2;
        }
    }
}

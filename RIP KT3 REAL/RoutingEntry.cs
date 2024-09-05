namespace RIP_KT3
{
    internal class RoutingEntry : ICloneable
    {
        public int networkID;
        public int interfaceID;
        public int hopCount;
        public int? learnedFromRouter;
        public int NotUpdatedFor; //In seconds

        public RoutingEntry (int networkID, int interfaceID, int hopCount, int? learnedFrom = null, int NotUpdatedFor = 0)
        {
            this.networkID = networkID;
            this.interfaceID = interfaceID;
            this.hopCount = hopCount;
            this.learnedFromRouter = learnedFrom;
            this.NotUpdatedFor = NotUpdatedFor;
        }

        public object Clone()
        {
            return new RoutingEntry(networkID, interfaceID, hopCount);
        }
    }
}

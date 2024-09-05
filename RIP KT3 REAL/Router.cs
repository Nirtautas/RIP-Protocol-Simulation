using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Timers;

namespace RIP_KT3
{
    internal class Router
    {
        private const int maxInterfaces = 8;
        private const int routeSharePeriod = 3000; //MIlls
        private const int invalidRouteCheckPeriod = 1000; //Mills
        private const int invalidRoutePeriod = 5; //Secs

        public int routerID { get; }
        public static int routerIDCounter = 101;

        private ICollection<RoutingEntry> routingTable;
        private Link?[] interfaceTable;

        private System.Timers.Timer routeShareTimer;
        private System.Timers.Timer invalidRouteTimer;
        private readonly object olock = new object();

        public Router()
        {
            routerID = routerIDCounter;
            ++routerIDCounter;

            routingTable = new List<RoutingEntry>();
            interfaceTable = new Link?[maxInterfaces];
            ClearInterfaceTable(interfaceTable);

            routeShareTimer = new(routeSharePeriod);
            routeShareTimer.Elapsed += OnRouteShare;
            routeShareTimer.AutoReset = true;
            routeShareTimer.Start();

            invalidRouteTimer = new(invalidRouteCheckPeriod);
            invalidRouteTimer.Elapsed += OnInvalidCheck;
            invalidRouteTimer.AutoReset = true;
            invalidRouteTimer.Start();
        }

        private void OnInvalidCheck(object? sender, ElapsedEventArgs e)
        {
            lock (olock)
            {
                for (int i = routingTable.Count - 1 ; i >= 0 ; i--) //Reverse for to enable deletes
                {
                    if (routingTable.ElementAt(i).learnedFromRouter != null) //If an entry is learned from another router
                    {
                        if (routingTable.ElementAt(i).NotUpdatedFor >= invalidRoutePeriod) //And is not updated for this period
                        {
                            routingTable.Remove(routingTable.ElementAt(i)); //Remove the routing entry
                        }
                        else
                        {
                            routingTable.ElementAt(i).NotUpdatedFor += 1; //Else increase the timer
                        }
                    }
                }
            }
        }

        private void OnRouteShare(Object? source, ElapsedEventArgs e)
        {
            for (int i = 0; i < maxInterfaces; ++i) //For all interfaces
            {
                if (interfaceTable[i] != null) //If interface is active
                {
                    if (interfaceTable[i].up == true) //And the network is up
                    {
                        //We get the destination router
                        Router dst = interfaceTable[i].GetOtherEndpoint(this);
                        //We get the interface which will accept the route table
                        int dstInterface = interfaceTable[i].GetOtherEndpointInterface(this);
                        //We clone the routing table
                        ICollection<RoutingEntry> rTable = CloneRoutingTable(routingTable);
                        //We select all entries that are not learned from destination router and are reachable
                        rTable = rTable.Where(t => t.learnedFromRouter == null && t.hopCount != 16).ToList();
                        //We send the routing table => Split horizon
                        dst.UpdateRoutingTable(dstInterface, routerID, rTable);
                    }
                }
            }
        }

        private void TriggeredUpdate()
        {
            for (int i = 0; i < maxInterfaces; ++i) //For all interfaces
            {
                if (interfaceTable[i] != null) //If interface is active
                {
                    if (interfaceTable[i].up == true) //And the network is up
                    {
                        //We get the destination router
                        Router dst = interfaceTable[i].GetOtherEndpoint(this);
                        //We get the interface which will accept the route table
                        int dstInterface = interfaceTable[i].GetOtherEndpointInterface(this);
                        //We clone the routing table
                        ICollection<RoutingEntry> rTable = CloneRoutingTable(routingTable);
                        //We select all entries that are invalid
                        rTable = rTable.Where(t => t.hopCount == 16).ToList();
                        //We send the routing table => Split horizon
                        dst.UpdateRoutingTable(dstInterface, routerID, rTable);
                    }
                }
            }
        }

        //Routing table
        public void UpdateRoutingTable(int receivingInterface, int incomingRouterID, ICollection<RoutingEntry> neighborRoutingTable)
        {
            lock (olock)
            {
                foreach (var neighborEntry in neighborRoutingTable) //For each neighboring entry
                {
                    var existingEntry = routingTable.FirstOrDefault(t => t.networkID == neighborEntry.networkID); //Find the existing entry

                    if (existingEntry != null) //If this entry exists
                    {
                        if (neighborEntry.hopCount == 16 && existingEntry.hopCount != 16 && existingEntry.learnedFromRouter != null) //If the neighboring entry says that some network is unreachable
                        {
                            existingEntry.interfaceID = receivingInterface; //Interface to reach the network is the incoming interface
                            existingEntry.hopCount = 16; //Hop count is set to invalid
                            existingEntry.learnedFromRouter = incomingRouterID; //Learned from neighbaring router
                            existingEntry.NotUpdatedFor = 0; //Reset timer
                            MarkInvalid(incomingRouterID); //Mark all routes through neighboring router invalid
                            TriggeredUpdate();
                        }
                        else if (neighborEntry.hopCount < existingEntry.hopCount  && existingEntry.learnedFromRouter != null) //If the neighboring entry is better than the existing one
                        {
                            existingEntry.interfaceID = receivingInterface;
                            existingEntry.hopCount = neighborEntry.hopCount + 1;
                            existingEntry.learnedFromRouter = incomingRouterID;
                            existingEntry.NotUpdatedFor = 0;
                        }
                    }
                    else //If the entry does not exist
                    {
                        if (neighborEntry.hopCount == 16)
                        {
                            routingTable.Add(new RoutingEntry(neighborEntry.networkID, receivingInterface, 16, incomingRouterID));
                        }
                        else
                        {
                            routingTable.Add(new RoutingEntry(neighborEntry.networkID, receivingInterface, neighborEntry.hopCount + 1, incomingRouterID));
                        }
                    }
                }
            }
        }

        public void MarkInvalid(int rID)
        {
            lock (olock)
            {
                foreach (var entry in routingTable)
                {
                    if (entry.learnedFromRouter == rID)
                        entry.hopCount = 16;
                }
            }
        }

        //Connections
        public void AddConnection(Link link)
        {
            lock (olock)
            {
                int? emptyIndex = link.GetEndpointInterface(this);
                if (emptyIndex != null)
                {
                    interfaceTable[(int)emptyIndex] = link;

                    RoutingEntry directEntry = new RoutingEntry(link.networkID, (int)emptyIndex, 0);
                    routingTable.Add(directEntry);
                }
            }
        }

        public static void LinkRouter(Router r1, Router r2, int cost = 0)
        {
            int? ix1 = r1.InterfaceTableFirstEmptyIndex();
            int? ix2 = r2.InterfaceTableFirstEmptyIndex();

            if (ix1 != null && ix2 != null)
            {
                Link link = new Link(r1, r2, (int)ix1, (int)ix2);
                r1.AddConnection(link);
                r2.AddConnection(link);
            }
        }

        public void RemoveLink(Link r)
        {
            for (int i = 0; i < maxInterfaces; ++i)
            {
                if (interfaceTable[i] == r)
                {
                    interfaceTable[i] = null;
                }
            }

            lock (olock)
            {
                for (int i = routingTable.Count - 1; i >= 0; i--)
                {
                    if (routingTable.ElementAt(i).networkID == r.networkID && routingTable.ElementAt(i).learnedFromRouter == null)
                    {
                        routingTable.Remove(routingTable.ElementAt(i));
                    }
                }
            }
        }

        public static void ShutdownNetwork(Router r1, Router r2)
        {
            Link? link = FindLink(r1, r2);
            if (link != null)
            {

                link.up = false;
                r1.Poison(link.GetEndpointInterface(r1));
                r2.Poison(link.GetEndpointInterface(r2));
                r1.TriggeredUpdate();
                r2.TriggeredUpdate();
                r1.RemoveLink(link);
                r2.RemoveLink(link);
            }
        }

        public static void ShutdownRouter(Router r1)
        {
            foreach (var link in r1.interfaceTable)
            {
                if (link != null)
                {
                    Router? otherEndpoint = link.GetOtherEndpoint(r1);
                    if (otherEndpoint != null)
                    {
                        link.up = false;
                        r1.Poison(link.GetEndpointInterface(r1));
                        otherEndpoint.Poison(link.GetEndpointInterface(otherEndpoint));
                        r1.TriggeredUpdate();
                        otherEndpoint.TriggeredUpdate();
                        r1.RemoveLink(link);
                        otherEndpoint.RemoveLink(link);
                    }
                }
            }
        }

        public void Poison(int interfaceID)
        {
            lock (olock)
            {
                foreach (var route in routingTable)
                {
                    if (route.interfaceID == interfaceID)
                    {
                        route.hopCount = 16;
                    }
                }
            }
        }

        public static Link? FindLink(Router r1, Router r2)
        {
            foreach (var link in r1.interfaceTable)
            {
                if (link.GetOtherEndpoint(r1) == r2)
                    return link;
            }
            return null;
        }

        //Interface
        public void ClearInterfaceTable(Link?[] interfaceTable)
        {
            for (int i = 0; i < maxInterfaces; ++i)
            {
                interfaceTable[i] = null;
            }
        }

        public int? InterfaceTableFirstEmptyIndex()
        {
            for (int i = 0; i < maxInterfaces; ++i)
            {
                if (interfaceTable[i] == null)
                    return i;
            }
            return null;
        }

        //Debug/Utility
        public void PrintRountingTable()

        {
            lock (olock)
            {
                Console.WriteLine("===R" + routerID + "===");
                foreach (var entry in routingTable)
                {
                    Console.WriteLine("Network - {0,2} Through Interface - {1} Hop Count - {2,2} Got from - {3,3} Last update - {4,3} ago."
                        , entry.networkID, entry.interfaceID, entry.hopCount, entry.learnedFromRouter, entry.NotUpdatedFor);
                }
                Console.WriteLine();
            }
        }

        public ICollection<RoutingEntry> CloneRoutingTable(ICollection<RoutingEntry> rtable)
        {
            ICollection<RoutingEntry> clone = new List<RoutingEntry>();

            lock (olock)
            {
                foreach (var entry in rtable)
                {
                    clone.Add((RoutingEntry)entry.Clone());
                }
                return clone;
            }
        }
    }
}

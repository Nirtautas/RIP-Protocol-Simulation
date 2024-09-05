namespace RIP_KT3;

class Program {
    static public void Main(string[] args)
    {
        
        Router r1 = new Router();
        Router r2 = new Router();
        Router r3 = new Router();
        Router r4 = new Router();
        Router r5 = new Router();

        Router.LinkRouter(r1, r2);
        Router.LinkRouter(r2, r3);
        Router.LinkRouter(r1, r3);
        Router.LinkRouter(r2, r4);
        Router.LinkRouter(r4, r5);

        PrintRouterInfo(new Router[] { r1, r2, r3, r4, r5});

        Thread.Sleep(10000);

        PrintRouterInfo(new Router[] { r1, r2, r3, r4, r5});

        Router.ShutdownNetwork(r2, r3);

        Thread.Sleep(10000);

        PrintRouterInfo(new Router[] { r1, r2, r3, r4, r5});

        Console.ReadLine();
    }

    public static void PrintRouterInfo(Router[] rlist)
    {
        foreach (Router r in rlist)
        {
            r.PrintRountingTable();
        }
    }
}
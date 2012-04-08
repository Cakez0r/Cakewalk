using System;
using System.Threading;

namespace Cakewalk.App
{
    class Program
    {
        static void Main(string[] args)
        {
            //Start up the world!
            World world = new World();
            world.Start();

            //Update metrics in the window title every second. Like a boss.
            //TODO: Do this properly!
            DateTime lastCheck = DateTime.Now;
            TimeSpan interval = TimeSpan.FromSeconds(1);
            while (true)
            {
                TimeSpan delta = DateTime.Now - lastCheck;
                if (delta >= interval)
                {
                    //Update every second or so
                    Console.Title = "IN: [" + NetEntity.IN + " Req/s | " + (NetEntity.BIN / 1024) + " KB/s]  OUT: [" + NetEntity.OUT + " Req/s | " + (NetEntity.BOUT / 1024) + " KB/s]  CCU: [" + world.CCU + "]  World Update Time: [" + world.LastUpdateDelta + "]";
                    NetEntity.IN = 0;
                    NetEntity.OUT = 0;
                    NetEntity.BOUT = 0;
                    NetEntity.BIN = 0;
                    lastCheck = DateTime.Now + (delta - interval);
                }

                Thread.Sleep(100);
            }
        }
    }
}

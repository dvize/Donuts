using System.Diagnostics;
using EFT;
using static Donuts.DonutComponent;

namespace Donuts.Models
{
    // Wrapper around method_10 called after bot creation, so we can pass it the BotCreationDataClass data

    internal class CreateBotCallbackWrapper
    {
        public BotCreationDataClass botData;
        public Stopwatch stopWatch = new Stopwatch();

        public void CreateBotCallback(BotOwner bot)
        {
            bool shallBeGroup = botData.SpawnParams?.ShallBeGroup != null;

            // I have no idea why BSG passes a stopwatch into this call...
            stopWatch.Start();
            methodCache["method_10"].Invoke(botSpawnerClass, new object[] { bot, botData, null, shallBeGroup, stopWatch });
        }
    }
}

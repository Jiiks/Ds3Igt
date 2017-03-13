using System;
using Ds3Igt;
using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(Ds3Factory))]
namespace Ds3Igt {

    class Ds3Factory : IComponentFactory {

        public ComponentCategory Category => ComponentCategory.Timer;

        public string ComponentName => "Dark Souls 3 IGT";
        public string UpdateName => ComponentName;
        public string Description => "Dark Souls 3 In-Game Time Splits by Jiiks https://jiiks.net";
        public Version Version => Version.Parse("1.3.2");

        public string XMLURL => "http://speedsouls.com/jiiks/livesplit/ds3/update.xml";
        public string UpdateURL => "http://speedsouls.com/jiiks/livesplit/ds3/update/";

        public IComponent Create(LiveSplitState state) => new Ds3Component(state);
    }
}

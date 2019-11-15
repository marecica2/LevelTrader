using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LevelTrader : Robot
    {
        [Parameter(DefaultValue = 0.0)]
        public double Parameter { get; set; }

        [Parameter(DefaultValue = 1, MinValue = -12, MaxValue = 12)]
        public int LevelTimeZoneOffset { get; set; }
        
        [Parameter(DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST\\2019-11-14 (46)\\FE_NT8.xml")]
        public string LevelFilePath { get; set; }

        private LevelController trader;

        private InputParams inputParams;

        protected override void OnStart()
        {
            inputParams = new InputParams
            {
                LevelFilePath = LevelFilePath,
                Parameter = Parameter,
                Instrument = Symbol.Name,
                TimeZoneOffset = LevelTimeZoneOffset,
            };

            trader = new LevelController(this, inputParams);
            trader.init();
        }

        protected override void OnTick()
        {
            trader.trade();
        }

        protected override void OnStop()
        {
            
        }
    }
}

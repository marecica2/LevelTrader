using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LevelTrader : Robot
    {
        [Parameter(DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST\\2019-11-14 (46)\\FE_NT8.xml", Group = "Input")]
        public string FilePath { get; set; }

        [Parameter(DefaultValue = 1, MinValue = -12, MaxValue = 12, Group = "Input")]
        public int TimeZoneOffset { get; set; }

        [Parameter(DefaultValue = 1, MinValue = 0.01, MaxValue = 100, Group = "Risk Management")]
        public double PositionSizePercents { get; set; }

        [Parameter(DefaultValue = 1000, MinValue = 10, Group = "Risk Management")]
        public int DefaultStopLossTicks { get; set; }
        
        [Parameter(DefaultValue = 1, MinValue = 0, Group = "Risk Management")]
        public double RiskRewardRatio { get; set; }

        [Parameter(DefaultValue = 0, MinValue = 0, MaxValue = 2, Group = "Trade Control")]
        public int Strategy_ID_SWING_INVEST { get; set; }

        [Parameter(DefaultValue = 33, MinValue = 0, MaxValue = 100, Group = "Trade Control")]
        public int ActivateLevelPercents { get; set; }

        [Parameter(DefaultValue = 77, MinValue = 0, MaxValue = 100, Group = "Trade Control")]
        public int DeactivateLevelPercents { get; set; }

        private LevelController trader;

        private InputParams inputParams;

        protected override void OnStart()
        {
            inputParams = new InputParams
            {
                LevelFilePath = FilePath,
                Instrument = Symbol.Name,
                TimeZoneOffset = TimeZoneOffset,
                LastPrice = Symbol.Bid,
                PositionSize = PositionSizePercents,
                StopLoss = DefaultStopLossTicks,
                RiskRewardRatio = RiskRewardRatio,
                Strategy = (StrategyType) Strategy_ID_SWING_INVEST,
                LevelActivate = ActivateLevelPercents,
                LevelDeactivate = DeactivateLevelPercents,

            };

            trader = new LevelController(this, inputParams);
            trader.Init();
        }

        protected override void OnTick()
        {
            trader.Trade();
        }

        protected override void OnStop()
        {
            
        }
    }
}

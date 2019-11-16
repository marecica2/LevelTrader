using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LevelTrader : Robot
    {
        [Parameter(DefaultValue = "C:\\Users\\marec\\Documents\\NinjaTrader 8\\bin\\MarketProfitPack\\CustomLevels\\", Group = "Input")]
        public string FilePath { get; set; }

        [Parameter(DefaultValue = "FE_NT8.xml", Group = "Input")]
        public string FileName { get; set; }

        [Parameter(DefaultValue = 1, MinValue = -12, MaxValue = 12, Group = "Input")]
        public int TimeZoneOffset { get; set; }

        [Parameter(DefaultValue = "08Robot.Server.TimeInUtc:35", Group = "Input")]
        public string DailyReloadTime { get; set; }

        [Parameter(DefaultValue = 1, MinValue = 0.01, MaxValue = 100, Group = "Risk Management")]
        public double PositionSizePercents { get; set; }

        [Parameter(DefaultValue = 100, MinValue = 10, Group = "Risk Management")]
        public int DefaultStopLossTicks { get; set; }
        
        [Parameter(DefaultValue = 1, MinValue = 0, Group = "Risk Management")]
        public double RiskRewardRatio { get; set; }

        [Parameter(DefaultValue = 0, MinValue = 0, MaxValue = 2, Group = "Trade Control")]
        public int Strategy_ID_SWING_INVEST { get; set; }

        [Parameter(DefaultValue = 33, MinValue = 0, MaxValue = 100, Group = "Trade Control")]
        public int ActivateLevelPercents { get; set; }

        [Parameter(DefaultValue = 77, MinValue = 0, MaxValue = 100, Group = "Trade Control")]
        public int DeactivateLevelPercents { get; set; }

        [Parameter(DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST", Group = "Backtest")]
        public string BackTestPath { get; set; }

        private LevelController trader;

        private InputParams inputParams;

        protected override void OnStart()
        {
            inputParams = new InputParams
            {
                LevelFilePath = FilePath,
                LevelFileName = FileName,
                Instrument = Symbol.Name,
                TimeZoneOffset = TimeZoneOffset,
                LastPrice = Symbol.Bid,
                PositionSize = PositionSizePercents,
                StopLoss = DefaultStopLossTicks,
                RiskRewardRatio = RiskRewardRatio,
                Strategy = (StrategyType)Strategy_ID_SWING_INVEST,
                LevelActivate = ActivateLevelPercents,
                LevelDeactivate = DeactivateLevelPercents,
                BackTestPath = BackTestPath,
                DailyReloadHour = int.Parse(DailyReloadTime.Split(new string[] { ":" }, StringSplitOptions.None)[0]),
                DailyReloadMinute = int.Parse(DailyReloadTime.Split(new string[] { ":" }, StringSplitOptions.None)[1]),
            };
            trader = new LevelController(this, inputParams);
            trader.Init();
        }

        protected override void OnTick()
        {
            trader.Trade();
        }


        protected override void OnBar()
        {
            trader.OnBar();
        }


        protected override void OnStop()
        {
            
        }

        
    }
}

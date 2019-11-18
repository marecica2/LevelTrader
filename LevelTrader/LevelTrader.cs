using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LevelTrader : Robot
    {
        [Parameter("Folder", DefaultValue = "C:\\Users\\marec\\Documents\\NinjaTrader 8\\bin\\MarketProfitPack\\CustomLevels\\", Group = "Input")]
        public string FilePath { get; set; }

        [Parameter("File Name", DefaultValue = "FE_NT8.xml", Group = "Input")]
        public string FileName { get; set; }

        [Parameter("Time Zone Offset", DefaultValue = 1, MinValue = -12, MaxValue = 12, Group = "Input")]
        public int TimeZoneOffset { get; set; }

        [Parameter("Daily Update Time" ,DefaultValue = "08:35", Group = "Input")]
        public string DailyReloadTime { get; set; }



        [Parameter("Position Size [%]" ,DefaultValue = 1, MinValue = 0.01, MaxValue = 5, Group = "Risk Management", Step = 1.0)]
        public double PositionSizePercents { get; set; }
        
        [Parameter("Risk Reward Ratio [%]", DefaultValue = 1, MinValue = 0, Group = "Risk Management", Step = 1.0)]
        public double RiskRewardRatio { get; set; }



        [Parameter("Activate Level Distance [%]", DefaultValue = 33, MinValue = 0, MaxValue = 100, Group = "Level Control")]
        public int ActivateLevelPercents { get; set; }

        [Parameter("Deactivate Level Distance [%]", DefaultValue = 77, MinValue = 0, MaxValue = 100, Group = "Level Control", Step = 1.0)]
        public int DeactivateLevelPercents { get; set; }

        [Parameter("Level Offset [Pips]", DefaultValue = 0, MinValue = -100, MaxValue = 100, Group = "Level Control")]
        public double LevelOffset { get; set; }



        [Parameter("Loss Strategy 0=Full Candles in Negative Area, 1=Candle Bodies in Negative Area, 2=POC in Negative Area", DefaultValue = 0, MinValue = 0, MaxValue = 1, Group = "Stop Loss Control")]
        public int LossStrategy { get; set; }

        [Parameter("Default Stop Loss [Pips]", DefaultValue = 10, MinValue = 1, Group = "Stop Loss Control")]
        public int DefaultStopLossPips { get; set; }

        [Parameter("Number of Candles in Negative Area", DefaultValue = 2, MinValue = 0, Group = "Stop Loss Control")]
        public int CandlesInNegativeArea { get; set; }

        [Parameter("Negative BE Offset [% of SL]", DefaultValue = 10, MinValue = -100, MaxValue = 100, Group = "Stop Loss Control", Step = 1.0)]
        public double NegativeBreakEvenOffset { get; set; }



        [Parameter("Profit Autoclose threshold [% of PT]", DefaultValue = 70, MinValue = 0, MaxValue = 100, Group = "Profit Control", Step = 1.0)]
        public double ProfitThreshold { get; set; }

        [Parameter("Profit Volume [% of PT]", DefaultValue = 50, MinValue = 0, MaxValue = 100, Group = "Profit Control", Step = 1.0)]
        public double ProfitVolume { get; set; }



        [Parameter("Backtest Folder", DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST", Group = "Backtest")]
        public string BackTestPath { get; set; }

        private LevelController LevelController;

        private PositionController PositionController;

        private InputParams InputParams;

        protected override void OnStart()
        {
            //RiskCalculator calc = new RiskCalculator(this);
            //double volume1 = calc.GetVolume("GBPUSD", 1, 12, TradeType.Buy);
            //Print("VOLUME " + volume1 );
            //double volume = calc.GetVolume("USDJPY", 1, 12, TradeType.Buy);
            //Print("VOLUME " + volume );

            InputParams = new InputParams
            {
                Instrument = Symbol.Name,
                LastPrice = Symbol.Bid,
                LevelFilePath = FilePath,
                LevelFileName = FileName,
                DailyReloadHour = int.Parse(DailyReloadTime.Split(new string[] { ":" }, StringSplitOptions.None)[0]),
                DailyReloadMinute = int.Parse(DailyReloadTime.Split(new string[] { ":" }, StringSplitOptions.None)[1]),
                TimeZoneOffset = TimeZoneOffset,

                PositionSize = PositionSizePercents,
                StopLoss = DefaultStopLossPips,
                RiskRewardRatio = RiskRewardRatio,

                LevelActivate = ActivateLevelPercents,
                LevelDeactivate = DeactivateLevelPercents,
                LevelOffset = LevelOffset,

                LossStrategy = (LossStrategy) LossStrategy,
                CandlesInNegativeArea = CandlesInNegativeArea,
                NegativeBreakEvenOffset = NegativeBreakEvenOffset,

                ProfitThreshold = ProfitThreshold,
                ProfitVolume = ProfitVolume,

                BackTestPath = BackTestPath,
            };
            LevelController = new LevelController(this, InputParams);
            LevelController.Init();

            PositionController = new PositionController(this, InputParams);
        }

        protected override void OnTick()
        {
            LevelController.OnTick();
            PositionController.OnTick();
        }


        protected override void OnBar()
        {
            LevelController.OnBar();
        }


        protected override void OnStop()
        {
            
        }

        
    }
}

using System;
using System.Collections.Generic;
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

        [Parameter("Time Zone Offset to UTC", DefaultValue = -1, MinValue = -12, MaxValue = 12, Group = "Input")]
        public int TimeZoneOffset { get; set; }

        [Parameter("Daily Update Time" ,DefaultValue = "08:35", Group = "Input")]
        public string DailyReloadTime { get; set; }



        [Parameter("Position Size [%]" ,DefaultValue = 1, MinValue = 0.01, MaxValue = 5, Group = "Risk Management", Step = 1.0)]
        public double PositionSizePercents { get; set; }
                
        [Parameter("Fixed Risk [Currency]", DefaultValue = 0, MinValue = 0, Group = "Risk Management", Step = 50.0)]
        public double FixedRisk { get; set; }



        [Parameter("Activate Level Distance [%]", DefaultValue = 33, MinValue = 0, MaxValue = 100, Group = "Level Control")]
        public int ActivateLevelPercents { get; set; }

        [Parameter("Deactivate Level Distance [%]", DefaultValue = 77, MinValue = 0, MaxValue = 100, Group = "Level Control", Step = 1.0)]
        public int DeactivateLevelPercents { get; set; }

        [Parameter("Level Offset [Pips]", DefaultValue = 0, MinValue = -100, MaxValue = 100, Group = "Level Control", Step = 1.0)]
        public double LevelOffset { get; set; }



        [Parameter("Loss Strategy 0=Full Candles in Negative Area, 1=Candle Bodies in Negative Area, 2=POC in Negative Area", DefaultValue = 0, MinValue = 0, MaxValue = 1, Group = "Loss Control")]
        public int LossStrategy { get; set; }

        [Parameter("Default Stop Loss [Pips]", DefaultValue = 10, MinValue = 1, Group = "Loss Control")]
        public int DefaultStopLossPips { get; set; }

        [Parameter("Number of Candles in Negative Area", DefaultValue = 2, MinValue = 0, Group = "Loss Control")]
        public int CandlesInNegativeArea { get; set; }

        [Parameter("Negative BE Offset [% of SL]", DefaultValue = 10, MinValue = -100, MaxValue = 100, Group = "Loss Control", Step = 1.0)]
        public double NegativeBreakEvenOffset { get; set; }



        [Parameter("Risk Reward Ratio [%]", DefaultValue = 1, MinValue = 0, Group = "Profit Control", Step = 1.0)]
        public double RiskRewardRatio { get; set; }

        [Parameter("Profit Autoclose threshold [% of PT]", DefaultValue = 70, MinValue = 0, MaxValue = 100, Group = "Profit Control", Step = 1.0)]
        public double ProfitThreshold { get; set; }

        [Parameter("Profit Volume [% of PT]", DefaultValue = 50, MinValue = 0, MaxValue = 100, Group = "Profit Control", Step = 1.0)]
        public double ProfitVolume { get; set; }



        [Parameter("Pause Trading on Calendar Events", DefaultValue = true, Group = "Event Calendar")]
        public bool CalendarPause { get; set; }

        [Parameter("Offset Before Event [min]", DefaultValue = 20, MinValue = 0, MaxValue = 60, Group = "Event Calendar")]
        public int CalendarBeforeOffset { get; set; }



        [Parameter("Backtest Folder", DefaultValue = "C:\\Users\\marec\\Documents\\TRADING_BACKTEST", Group = "Backtest")]
        public string BackTestPath { get; set; }



        private LevelController LevelController;

        private Calendar Calendar;

        private PositionController PositionController;

        private InputParams InputParams;

        protected override void OnStart()
        {
            InputParams = new InputParams
            {
                Instrument = Symbol.Name,
                LastPrice = Symbol.Bid,
                LevelFilePath = FilePath,
                LevelFileName = FileName,
                DailyReloadHour = int.Parse(DailyReloadTime.Split(new string[] { ":" }, StringSplitOptions.None)[0]),
                DailyReloadMinute = int.Parse(DailyReloadTime.Split(new string[] { ":" }, StringSplitOptions.None)[1]),
                TimeZoneOffset = TimeZoneOffset,

                PositionSize = PositionSizePercents * 0.01,
                FixedRiskAmount = FixedRisk,

                LevelActivate = ActivateLevelPercents * 0.01,
                LevelDeactivate = DeactivateLevelPercents * 0.01,
                LevelOffset = LevelOffset,

                LossStrategy = (LossStrategy)LossStrategy,
                StopLossPips = DefaultStopLossPips,
                CandlesInNegativeArea = CandlesInNegativeArea,
                NegativeBreakEvenOffset = NegativeBreakEvenOffset * 0.01,

                RiskRewardRatio = RiskRewardRatio * 0.01,
                ProfitThreshold = ProfitThreshold * 0.01,
                ProfitVolume = ProfitVolume * 0.01,

                CalendarPause = CalendarPause,
                CalendarBeforeOffset = CalendarBeforeOffset,

                BackTestPath = BackTestPath,
            };

            Calendar = new Calendar(this, InputParams);
            Calendar.Init();
            LevelController = new LevelController(this, InputParams, Calendar);
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
